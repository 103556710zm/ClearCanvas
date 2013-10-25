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
using System.Linq;
using ClearCanvas.Common;
using ClearCanvas.Common.Utilities;
using ClearCanvas.Desktop;
using ClearCanvas.Desktop.Actions;
using ClearCanvas.ImageViewer.BaseTools;
using ClearCanvas.ImageViewer.Common.WorkItem;
using ClearCanvas.ImageViewer.Graphics;
using ClearCanvas.ImageViewer.PresentationStates.Dicom;
using ClearCanvas.ImageViewer.StudyManagement;

namespace ClearCanvas.ImageViewer.Tools.Reporting.KeyImages
{
	[MenuAction("create", "imageviewer-contextmenu/MenuCreateKeyImage", "Create")]
	[ButtonAction("create", "global-toolbars/ToolbarStandard/ToolbarCreateKeyImage", "Create", KeyStroke = XKeys.Space)]
	[Tooltip("create", "TooltipCreateKeyImage")]
	[IconSet("create", "Icons.CreateKeyImageToolSmall.png", "Icons.CreateKeyImageToolMedium.png", "Icons.CreateKeyImageToolLarge.png")]
	[EnabledStateObserver("create", "Enabled", "EnabledChanged")]
	// TODO (CR Phoenix5 - Med): Clinical as well
	[ViewerActionPermission("create", AuthorityTokens.Study.KeyImages)]
	//
	[ButtonAction("show", "global-toolbars/ToolbarStandard/ToolbarShowKeyImages", "Show")]
	[Tooltip("show", "TooltipShowKeyImages")]
	[IconSet("show", "Icons.ShowKeyImagesToolSmall.png", "Icons.ShowKeyImagesToolMedium.png", "Icons.ShowKeyImagesToolLarge.png")]
	[EnabledStateObserver("show", "ShowEnabled", "ShowEnabledChanged")]
	// TODO (CR Phoenix5 - Med): Clinical as well
	[ViewerActionPermission("show", AuthorityTokens.Study.KeyImages)]
	//
	[ExtensionOf(typeof (ImageViewerToolExtensionPoint))]
	internal class KeyImageTool : ImageViewerTool
	{
		#region Private Fields

		private readonly FlashOverlayController _flashOverlayController;
		private bool _showEnabled;
		private bool _firstKeyImageCreation = true;
		private event EventHandler _showEnabledChanged;
		private IWorkItemActivityMonitor _workItemActivityMonitor;

		#endregion

		public KeyImageTool()
		{
			_flashOverlayController = new FlashOverlayController("Icons.CreateKeyImageToolLarge.png", new ApplicationThemeResourceResolver(GetType(), false));
		}

		public bool ShowEnabled
		{
			get { return _showEnabled; }
			set
			{
				if (_showEnabled == value)
					return;

				_showEnabled = value;
				EventsHelper.Fire(_showEnabledChanged, this, EventArgs.Empty);
			}
		}

		public event EventHandler ShowEnabledChanged
		{
			add { _showEnabledChanged += value; }
			remove { _showEnabledChanged -= value; }
		}

		#region Overrides

		public override void Initialize()
		{
			base.Initialize();
			KeyImageClipboard.OnViewerOpened(Context.Viewer);

			UpdateEnabled();

			if (WorkItemActivityMonitor.IsSupported)
			{
				_workItemActivityMonitor = WorkItemActivityMonitor.Create();
				_workItemActivityMonitor.IsConnectedChanged += OnIsConnectedChanged;
			}

			if (!KeyImageClipboardComponent.HasViewPlugin)
			{
				foreach (var buttonAction in Actions.Where(a => a.ActionID == "show").OfType<ClickAction>())
					buttonAction.Visible = false;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (_workItemActivityMonitor != null)
			{
				_workItemActivityMonitor.IsConnectedChanged -= OnIsConnectedChanged;
				_workItemActivityMonitor.Dispose();
				_workItemActivityMonitor = null;
			}

			if (Context != null)
			{
				KeyImageClipboard.OnViewerClosed(Context.Viewer);
			}

			base.Dispose(disposing);
		}

		/// <remarks>
		/// The current implementation of <see cref="KeyImagePublisher"/> supports only locally stored images that are <see cref="IImageSopProvider"/>s and supports <see cref="DicomSoftcopyPresentationState"/>s.
		/// </remarks>
		private static bool IsSupportedImage(IPresentationImage image)
		{
			var imageSopProvider = image as IImageSopProvider;
			if (imageSopProvider == null)
				return false;
			return imageSopProvider.ImageSop.IsStored && DicomSoftcopyPresentationState.IsSupported(image);
		}

		private void UpdateEnabled()
		{
			Enabled = IsSupportedImage(SelectedPresentationImage) &&
			          // TODO (CR Phoenix5 - Med): Clinical as well	  
			          PermissionsHelper.IsInRole(AuthorityTokens.Study.KeyImages);

			// TODO (CR Phoenix5 - Med): Clinical as well
			ShowEnabled = PermissionsHelper.IsInRole(AuthorityTokens.Study.KeyImages);
		}

		private void OnIsConnectedChanged(object sender, EventArgs eventArgs)
		{
			UpdateEnabled();
		}

		protected override void OnPresentationImageSelected(object sender, PresentationImageSelectedEventArgs e)
		{
			UpdateEnabled();
		}

		protected override void OnTileSelected(object sender, TileSelectedEventArgs e)
		{
			UpdateEnabled();
		}

		#endregion

		#region Methods

		public void Show()
		{
			if (ShowEnabled)
				KeyImageClipboard.Show(Context.DesktopWindow);
		}

		public void Create()
		{
			if (!Enabled)
				return;

			if (KeyImageClipboardComponent.HasViewPlugin)
			{
				try
				{
					IPresentationImage image = Context.Viewer.SelectedPresentationImage;
					if (image != null)
					{
						KeyImageClipboard.Add(image);
						_flashOverlayController.Flash(image);
					}

					if (_firstKeyImageCreation && ShowEnabled)
					{
						KeyImageClipboard.Show(Context.DesktopWindow, ShelfDisplayHint.DockAutoHide | ShelfDisplayHint.DockLeft);
						_firstKeyImageCreation = false;
					}
				}
				catch (Exception ex)
				{
					Platform.Log(LogLevel.Error, ex, "Failed to add item to the key image clipboard.");
					ExceptionHandler.Report(ex, SR.MessageCreateKeyImageFailed, Context.DesktopWindow);
				}
			}
			else
			{
				try
				{
					IPresentationImage image = Context.Viewer.SelectedPresentationImage;
					if (image != null)
					{
						// Still save to clipboard, makes publishing easier later
						KeyImageClipboard.Add(image);

						_flashOverlayController.Flash(image);
					}
				}
				catch (Exception ex)
				{
					Platform.Log(LogLevel.Error, ex, "Failed to create virtual display set for key image.");
					ExceptionHandler.Report(ex, SR.MessageCreateKeyImageFailed, Context.DesktopWindow);
				}
			}
		}

		#endregion
	}
}