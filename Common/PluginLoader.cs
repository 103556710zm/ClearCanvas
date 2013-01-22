﻿#region License

// Copyright (c) 2011, ClearCanvas Inc.
// All rights reserved.
// http://www.clearcanvas.ca
//
// This software is licensed under the Open Software License v3.0.
// For the complete license, see http://www.clearcanvas.ca/OSLv3.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClearCanvas.Common.Utilities;

namespace ClearCanvas.Common
{
	/// <summary>
	/// Encapsulates logic for loading plugins from disk.
	/// </summary>
	internal class PluginLoader
	{
		class PluginFile
		{
			public PluginFile(FileInfo fileInfo, PluginAttribute pluginAttribute, Assembly assembly)
			{
				FileInfo = fileInfo;
				PluginAttribute = pluginAttribute;
				Assembly = assembly;
			}

			public readonly FileInfo FileInfo;
			public readonly PluginAttribute PluginAttribute;
			public readonly Assembly Assembly;
		}

		private static readonly string _metadataCacheFileName = string.Format("{0}\\pxpxcache", Platform.ApplicationDataDirectory);
		private readonly string _pluginDir;

		internal PluginLoader(string pluginDir)
		{
			_pluginDir = pluginDir;
		}

		public event EventHandler<PluginProcessedEventArgs> PluginProcessed;

		public List<PluginInfo> LoadPlugins()
		{
			EventsHelper.Fire(PluginProcessed, this, new PluginProcessedEventArgs(SR.MessageFindingPlugins, null));

			var pluginFiles = LoadPluginFiles();
			List<PluginInfo> pluginInfos;
			if (!TryLoadCachedMetadata(pluginFiles, out pluginInfos))
			{
				pluginInfos = BuildMetadata(pluginFiles);
				SaveCachedMetadata(pluginInfos);
			}

			return pluginInfos;
		}

		private List<PluginFile> LoadPluginFiles()
		{
			var plugins = new List<PluginFile>();
			FileProcessor.Process(_pluginDir, "*.dll", path =>
			{
				PluginFile file;
				if (LoadPlugin(path, out file))
				{
					plugins.Add(file);
					var e = new PluginProcessedEventArgs(string.Format(SR.FormatLoadedPlugin, file.FileInfo.Name), file.Assembly);
					EventsHelper.Fire(PluginProcessed, this, e);
				}
			}, true);

			return plugins;
		}

		private static bool TryLoadCachedMetadata(List<PluginFile> pluginFiles, out List<PluginInfo> pluginInfos)
		{
			if (IsCachedMetadataValid(pluginFiles))
			{
				try
				{
					var pluginLookup = pluginFiles.ToDictionary(p => p.Assembly.GetName().Name, p => p.Assembly);
					pluginInfos = PluginInfoCache.Read(_metadataCacheFileName, name => pluginLookup[name]);
					return true;
				}
				catch (Exception)
				{
					// not a big deal, it just means the cache isn't accessible right now
					// (maybe another app domain or process is writing to it?)
					// and we need to build meta-data from the binaries
					Platform.Log(LogLevel.Debug, "Failed to read plugin metadata cache.");
				}
			}
			pluginInfos = null;
			return false;
		}

		private static bool IsCachedMetadataValid(IEnumerable<PluginFile> pluginFiles)
		{
			var cacheFile = new FileInfo(_metadataCacheFileName);
			if (!cacheFile.Exists)
				return false;

			// cache must be newer than last write time of any config file
			var configFiles = Directory.GetFiles(Platform.InstallDirectory, "*.config", SearchOption.TopDirectoryOnly);
			if (!configFiles.All(cf => File.GetLastWriteTime(cf) < cacheFile.LastWriteTime))
				return false;

			// cache must be newer than last write time of any plugin file
			if (!pluginFiles.All(p => p.FileInfo.LastWriteTime < cacheFile.LastWriteTime))
				return false;

			return true;
		}

		private List<PluginInfo> BuildMetadata(IEnumerable<PluginFile> pluginFiles)
		{
			var results = new List<PluginInfo>();
			foreach (var p in pluginFiles)
			{
				var e = new PluginProcessedEventArgs(string.Format(SR.FormatProcessingPlugin, p.FileInfo.Name), p.Assembly);
				EventsHelper.Fire(PluginProcessed, this, e);

				// the metadata is built in the PluginInfo constructor
				var pi = new PluginInfo(p.Assembly, p.PluginAttribute.Name, p.PluginAttribute.Description, p.PluginAttribute.Icon);
				results.Add(pi);
			}
			return results;
		}


		private static void SaveCachedMetadata(List<PluginInfo> pluginInfos)
		{
			try
			{
				PluginInfoCache.Write(_metadataCacheFileName, pluginInfos);
			}
			catch (Exception)
			{
				// not a big deal, it just means the cache won't be updated this time around
				Platform.Log(LogLevel.Debug, "Failed to write plugin metadata cache.");
			}
		}

		private static bool LoadPlugin(string path, out PluginFile pluginFile)
		{
			try
			{
				// load assembly
				var asm = Assembly.LoadFrom(path);

				// is it a plugin??
				var pluginAttr = (PluginAttribute)asm.GetCustomAttributes(typeof(PluginAttribute), false).FirstOrDefault();
				if (pluginAttr != null)
				{
					pluginFile = new PluginFile(new FileInfo(path), pluginAttr, asm);
					return true;
				}
			}
			catch (BadImageFormatException e)
			{
				// unmanaged DLL in the plugin directory
				Platform.Log(LogLevel.Debug, SR.LogFoundUnmanagedDLL, e.FileName);
			}
			catch (ReflectionTypeLoadException e)
			{
				// this exception usually means one of the dependencies is missing
				Platform.Log(LogLevel.Error, SR.LogFailedToProcessPluginAssembly, Path.GetFileName(path));

				// log a detail message for each missing dependency
				foreach (var loaderException in e.LoaderExceptions)
				{
					// just log the message, don't need the full stack trace
					Platform.Log(LogLevel.Error, loaderException.Message);
				}
			}
			catch (FileNotFoundException e)
			{
				Platform.Log(LogLevel.Error, e, "File not found while loading plugin: {0}", path);
			}
			catch (Exception e)
			{
				// there was a problem processing this assembly
				Platform.Log(LogLevel.Error, e, SR.LogFailedToProcessPluginAssembly, path);
			}

			pluginFile = null;
			return false;
		}
	}
}
