#region License

// Copyright (c) 2013, ClearCanvas Inc.
// All rights reserved.
// http://www.clearcanvas.ca
//
// This file is part of the ClearCanvas RIS/PACS open source project.
//
// The ClearCanvas RIS/PACS open source project is free software: you can
// redistribute it and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// The ClearCanvas RIS/PACS open source project is distributed in the hope that it
// will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with
// the ClearCanvas RIS/PACS open source project.  If not, see
// <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using ClearCanvas.Common;
using ClearCanvas.Common.Utilities;
using ClearCanvas.Desktop;
using ClearCanvas.Desktop.Actions;
using ClearCanvas.ImageViewer.BaseTools;

namespace ClearCanvas.ImageViewer.Layout.Basic
{
    [ActionFormerly("dropdown", "ClearCanvas.ImageViewer.Tools.Standard.ShowHideOverlaysTool:dropdown")]
    [DropDownButtonAction("dropdown", "global-toolbars/ToolbarStandard/ToolbarShowHideOverlays", "ToggleAll", "DropDownActionModel", KeyStroke = XKeys.O)]
    [TooltipValueObserver("dropdown", "Tooltip", "TooltipChanged")]
	[GroupHint("dropdown", "Tools.Image.Overlays.Text.ShowHide")]
    [IconSetObserver("dropdown", "IconSet", "IconSetChanged")]
    [ExtensionOf(typeof(ImageViewerToolExtensionPoint))]
	public class ShowHideOverlaysTool : ImageViewerTool
	{
        private enum ShowHideOption
        {
            ShowSelected,
            HideAll,
            HideUnimportant
        }

        private readonly IconSet _selectedOverlaysVisible;
        private readonly IconSet _selectedOverlaysHidden;
        private ActionSet _nilActions;

        public ShowHideOverlaysTool()
        {
            _selectedOverlaysVisible = new IconSet("Icons.ShowHideOverlaysToolSmall.png", "Icons.ShowHideOverlaysToolMedium.png", "Icons.ShowHideOverlaysToolLarge.png");
            _selectedOverlaysHidden = new UnavailableActionIconSet(_selectedOverlaysVisible){GrayMode = true};
            IconSet = _selectedOverlaysVisible;

            try
            {
                new SelectOverlaysActionViewExtensionPoint().CreateExtension();
                IsViewSupported = true;
            }
            catch (NotSupportedException)
            {
                IsViewSupported = false;
            }
		}

        public static bool IsViewSupported { get; private set; }

        internal IImageViewer Viewer { get { return Context.Viewer; } }

        public override IActionSet Actions
        {
            get
            {
                if (!IsViewSupported)
                {
                    if (_nilActions == null)
                    {
                        _nilActions = new ActionSet();
                        base.Actions = _nilActions;
                    }
                }

                return base.Actions;
            }
            protected set
            {
                base.Actions = value;
            }
        }

		public ActionModelNode DropDownActionModel
		{
			get
			{
                //Take advantage of the fact that the drop-down model is requested each time it's going to be shown.
                var resolver = new ActionResourceResolver(typeof(SelectOverlaysAction));
                var action = new SelectOverlaysAction(this, "selectOverlays", new ActionPath("overlays-dropdown/SelectOverlays", resolver), resolver);
                var actionSet = new ActionSet(new[] { action });
				return ActionModelRoot.CreateModel(typeof (ShowHideOverlaysTool).Namespace, "overlays-dropdown", actionSet);
			}
		}

        public IconSet IconSet { get; private set; }
        public event EventHandler IconSetChanged
        {
            add { SelectedOverlaysVisibleChanged += value; }
            remove { SelectedOverlaysVisibleChanged -= value; }
        }

        public string Tooltip
        {
            get { return SelectedOverlaysVisible ? SR.TooltipHideOverlays : SR.TooltipShowOverlays; }
        }

        public event EventHandler TooltipChanged
        {
            add { SelectedOverlaysVisibleChanged += value; }
            remove { SelectedOverlaysVisibleChanged -= value; }
        }

        public bool SelectedOverlaysVisible
        {
            get { return ReferenceEquals(IconSet, _selectedOverlaysVisible); }
        }

        public event EventHandler SelectedOverlaysVisibleChanged;

        public override void Initialize()
        {
            base.Initialize();

            DisplaySetCreationSettings.DefaultInstance.PropertyChanged += SettingsChanged;
            Context.Viewer.EventBroker.DisplaySetChanged += OnDisplaySetChanged;
        }

        protected override void Dispose(bool disposing)
        {
            DisplaySetCreationSettings.DefaultInstance.PropertyChanged -= SettingsChanged;
            Context.Viewer.EventBroker.DisplaySetChanged -= OnDisplaySetChanged;
            base.Dispose(disposing);
        }

        public void ToggleAll()
        {
            IconSet = SelectedOverlaysVisible ? _selectedOverlaysHidden : _selectedOverlaysVisible;
            EventsHelper.Fire(SelectedOverlaysVisibleChanged, this, EventArgs.Empty);

            var selectedOverlaysVisible = SelectedOverlaysVisible;
            foreach (var imageBox in base.Context.Viewer.PhysicalWorkspace.ImageBoxes.Where(i => i.DisplaySet != null))
                UpdateVisibility(imageBox.DisplaySet, selectedOverlaysVisible ? ShowHideOption.ShowSelected : ShowHideOption.HideAll);

			Context.Viewer.PhysicalWorkspace.Draw();
		}

        private void OnDisplaySetChanged(object sender, DisplaySetChangedEventArgs e)
        {
            if (e.NewDisplaySet == null)
                return;

            var clock = new CodeClock();
            clock.Start();

            UpdateVisibility(e.NewDisplaySet, SelectedOverlaysVisible ? ShowHideOption.ShowSelected : ShowHideOption.HideUnimportant);

            clock.Stop();
            Trace.WriteLine(String.Format("{0} - UpdateVisibility took {1}", GetType().FullName, clock.Seconds));
        }

        private static void UpdateVisibility(IDisplaySet displaySet, ShowHideOption option)
        {
            if (displaySet == null)
                return;

            //Have to update all images each time so that even ones that haven't been drawn are correct.
            //That way, even ones that are exported to the clipboard look right.
            switch (option)
            {
                case ShowHideOption.HideUnimportant:
                    foreach (var image in displaySet.PresentationImages)
                        image.GetOverlays().HideUnimportant(false);
                    break;
                case ShowHideOption.ShowSelected:
                    foreach (var image in displaySet.PresentationImages)
                        image.GetOverlays().ShowSelected(false);
                    break;
                case ShowHideOption.HideAll:
                    foreach (var image in displaySet.PresentationImages)
                        image.GetOverlays().HideAll(false);
                    break;
            }
        }

        private void SettingsChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            //OverlayHelper caches the modality defaults in the viewer extension data for efficiency.
            OverlayHelper.OverlaySettingsChanged(Context.Viewer);
        }
	}
}