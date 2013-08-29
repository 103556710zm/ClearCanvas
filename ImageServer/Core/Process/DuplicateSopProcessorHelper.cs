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
using System.IO;
using ClearCanvas.Common;
using ClearCanvas.Dicom;
using ClearCanvas.Dicom.Network;
using ClearCanvas.Dicom.Utilities.Command;
using ClearCanvas.Enterprise.Core;
using ClearCanvas.ImageServer.Common;
using ClearCanvas.ImageServer.Common.WorkQueue;
using ClearCanvas.ImageServer.Core.Command;
using ClearCanvas.ImageServer.Enterprise.Command;
using ClearCanvas.ImageServer.Model;
using ClearCanvas.ImageServer.Model.EntityBrokers;
using SaveDicomFileCommand = ClearCanvas.ImageServer.Core.Command.SaveDicomFileCommand;

namespace ClearCanvas.ImageServer.Core.Process
{
	/// <summary>
	/// Provides helper method to process duplicates.
	/// </summary>
	public static class DuplicateSopProcessorHelper
	{
		#region Public Methods

		/// <summary>
		/// Process the duplicate with the supplied <see cref="DuplicateProcessingEnum"/>
		/// </summary>
		/// <param name="context">The processing context</param>
		/// <param name="file">The file</param>
		/// <param name="data">The data</param>
		/// <param name="duplicate">How the processor should handle the duplicate</param>
		public static void ProcessStoredDuplicate(SopInstanceProcessorContext context,
		                                          DicomFile file,
		                                          StudyProcessWorkQueueData data,
		                                          DuplicateProcessingEnum duplicate)
		{
			SaveDuplicate(context, file);
			var uidData = new WorkQueueUidData
				{
					Extension = ServerPlatform.DuplicateFileExtension,
					GroupId = context.Group,
					DuplicateProcessing = duplicate
				};

			if (context.Request != null)
				uidData.OperationToken = context.Request.OperationToken;

			context.CommandProcessor.AddCommand(
				new UpdateWorkQueueCommand(file, context.StudyLocation, true, data, uidData, context.Request));
		}

		/// <summary>
		/// Inserts the duplicate DICOM file into the <see cref="WorkQueue"/> for processing (if applicable).
		/// </summary>
		/// <param name="context">The processing context.</param>
		/// <param name="file">The duplicate DICOM file being processed.</param>
		/// <param name="data">Extra data to insert for the WorkQueue item.</param>
		/// <returns>A <see cref="DicomProcessingResult"/> that contains the result of the processing.</returns>
		/// <remarks>
		/// This method inserts a <see cref="CommandBase"/> into <paramref name="context.CommandProcessor"/>.
		/// The outcome of the operation depends on the <see cref="DuplicateSopPolicyEnum"/> of the <see cref="ServerPartition"/>.
		/// If it is set to <see cref="DuplicateSopPolicyEnum.CompareDuplicates"/>, the duplicate file will be
		/// inserted into the <see cref="WorkQueue"/> for processing.
		/// </remarks>
		public static DicomProcessingResult Process(SopInstanceProcessorContext context, DicomFile file,
		                                            StudyProcessWorkQueueData data)
		{
			Platform.CheckForNullReference(file, "file");
			Platform.CheckForNullReference(context, "context");
			Platform.CheckMemberIsSet(context.Group, "parameters.Group");
			Platform.CheckMemberIsSet(context.CommandProcessor, "parameters.CommandProcessor");
			Platform.CheckMemberIsSet(context.StudyLocation, "parameters.StudyLocation");

			var result = new DicomProcessingResult
				{
					DicomStatus = DicomStatuses.Success,
					Successful = true,
					StudyInstanceUid = file.DataSet[DicomTags.StudyInstanceUid].GetString(0, string.Empty),
					SeriesInstanceUid = file.DataSet[DicomTags.SeriesInstanceUid].GetString(0, string.Empty),
					SopInstanceUid = file.DataSet[DicomTags.SopInstanceUid].GetString(0, string.Empty),
					SopClassUid = file.DataSet[DicomTags.SopClassUid].GetString(0, string.Empty),
					AccessionNumber = file.DataSet[DicomTags.AccessionNumber].GetString(0, string.Empty)
				};

			string failureMessage;

			if (SopClassIsReport(result.SopClassUid) && context.StudyLocation.ServerPartition.AcceptLatestReport)
			{
				Platform.Log(LogLevel.Info, "Duplicate Report received, overwriting {0}", result.SopInstanceUid);
				ProcessStoredDuplicate(context, file, data, DuplicateProcessingEnum.OverwriteReport);
				return result;
			}

			if (DuplicatePolicy.IsParitionDuplicatePolicyOverridden(context.StudyLocation))
			{
				Platform.Log(LogLevel.Warn,
				             "Duplicate instance received for study {0} on Partition {1}. Duplicate policy overridden. Will overwrite {2}",
				             result.StudyInstanceUid, context.StudyLocation.ServerPartition.AeTitle, result.SopInstanceUid);
				ProcessStoredDuplicate(context, file, data, DuplicateProcessingEnum.OverwriteSop);
				return result;
			}

			if (context.StudyLocation.ServerPartition.DuplicateSopPolicyEnum.Equals(DuplicateSopPolicyEnum.SendSuccess))
			{
				Platform.Log(LogLevel.Info, "Duplicate SOP Instance received, sending success response {0}", result.SopInstanceUid);
				return result;
			}

			if (context.StudyLocation.ServerPartition.DuplicateSopPolicyEnum.Equals(DuplicateSopPolicyEnum.RejectDuplicates))
			{
				failureMessage = String.Format("Duplicate SOP Instance received, rejecting {0}", result.SopInstanceUid);
				Platform.Log(LogLevel.Info, failureMessage);
				result.SetError(DicomStatuses.DuplicateSOPInstance, failureMessage);
				return result;
			}

			if (context.StudyLocation.ServerPartition.DuplicateSopPolicyEnum.Equals(DuplicateSopPolicyEnum.CompareDuplicates))
			{
				ProcessStoredDuplicate(context, file, data, DuplicateProcessingEnum.Compare);
			}
			else
			{
				failureMessage = String.Format("Duplicate SOP Instance received. Unsupported duplicate policy {0}.",
				                               context.StudyLocation.ServerPartition.DuplicateSopPolicyEnum);
				result.SetError(DicomStatuses.DuplicateSOPInstance, failureMessage);
				return result;
			}

			return result;
		}

		/// <summary>
		/// Create Duplicate SIQ Entry
		/// </summary>
		/// <param name="file"></param>
		/// <param name="location"></param>
		/// <param name="sourcePath"></param>
		/// <param name="queue"></param>
		/// <param name="uid"></param>
		/// <param name="data"></param>
		public static void CreateDuplicateSIQEntry(DicomFile file, StudyStorageLocation location, string sourcePath,
		                                           WorkQueue queue, WorkQueueUid uid, StudyProcessWorkQueueData data)
		{
			Platform.Log(LogLevel.Info, "Creating Work Queue Entry for duplicate...");
			String uidGroup = queue.GroupID ?? queue.GetKey().Key.ToString();
			using (var commandProcessor = new ServerCommandProcessor("Insert Work Queue entry for duplicate"))
			{
				commandProcessor.AddCommand(new FileDeleteCommand(sourcePath, true));

				var sopProcessingContext = new SopInstanceProcessorContext(commandProcessor, location, uidGroup);
				DicomProcessingResult result = Process(sopProcessingContext, file, data);
				if (!result.Successful)
				{
					FailUid(uid, true);
					return;
				}

				commandProcessor.AddCommand(new DeleteWorkQueueUidCommand(uid));

				if (!commandProcessor.Execute())
				{
					Platform.Log(LogLevel.Error, "Unexpected error when creating duplicate study integrity queue entry: {0}",
					             commandProcessor.FailureReason);
					FailUid(uid, true);
				}
			}
		}

		public static bool SopClassIsReport(string sopClassUid)
		{
			return (SopClass.EncapsulatedPdfStorageUid.Equals(sopClassUid)
			        || SopClass.EncapsulatedCdaStorageUid.Equals(sopClassUid));
		}

		#endregion

		#region Private Methods

		private static void FailUid(WorkQueueUid sop, bool retry)
		{
			using (
				IUpdateContext updateContext =
					PersistentStoreRegistry.GetDefaultStore().OpenUpdateContext(UpdateContextSyncMode.Flush))
			{
				var uidUpdateBroker = updateContext.GetBroker<IWorkQueueUidEntityBroker>();
				var columns = new WorkQueueUidUpdateColumns();
				if (!retry)
					columns.Failed = true;
				else
				{
					if (sop.FailureCount >= ImageServerCommonConfiguration.WorkQueueMaxFailureCount)
					{
						columns.Failed = true;
					}
					else
					{
						columns.FailureCount = sop.FailureCount++;
					}
				}

				uidUpdateBroker.Update(sop.GetKey(), columns);
				updateContext.Commit();
			}
		}

		private static void SaveDuplicate(SopInstanceProcessorContext context, DicomFile file)
		{
			String sopUid = file.DataSet[DicomTags.SopInstanceUid].ToString();

			String path = Path.Combine(context.StudyLocation.FilesystemPath, context.StudyLocation.PartitionFolder);
			context.CommandProcessor.AddCommand(new CreateDirectoryCommand(path));

			path = Path.Combine(path, ServerPlatform.ReconcileStorageFolder);
			context.CommandProcessor.AddCommand(new CreateDirectoryCommand(path));

			path = Path.Combine(path, context.Group /* the AE title + timestamp */);
			context.CommandProcessor.AddCommand(new CreateDirectoryCommand(path));

			path = Path.Combine(path, context.StudyLocation.StudyInstanceUid);
			context.CommandProcessor.AddCommand(new CreateDirectoryCommand(path));

			path = Path.Combine(path, sopUid);
			path += "." + ServerPlatform.DuplicateFileExtension;

			context.CommandProcessor.AddCommand(new SaveDicomFileCommand(path, file, true));

			Platform.Log(ServerPlatform.InstanceLogLevel, "Duplicate ==> {0}", path);
		}

		#endregion
	}
}