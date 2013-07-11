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

using ClearCanvas.ImageViewer.Mathematics;

namespace ClearCanvas.ImageViewer.Volumes
{
	/// <summary>
	/// Represents the common header data of the <see cref="Volume"/>.
	/// </summary>
	public interface IVolumeHeader
	{
		/// <summary>
		/// Gets the dimensions of the volume data.
		/// </summary>
		Size3D ArrayDimensions { get; }

		/// <summary>
		/// Gets whether or not the values of the volume data are signed.
		/// </summary>
		bool Signed { get; }

		/// <summary>
		/// Gets the number of bits per voxel of the volume data.
		/// </summary>
		int BitsPerVoxel { get; }

		/// <summary>
		/// Gets the effective size of the volume (that is, the <see cref="ArrayDimensions"/> multiplied by the <see cref="VoxelSpacing"/> in each respective dimension).
		/// </summary>
		Vector3D VolumeSize { get; }

		/// <summary>
		/// Gets the effective bounds of the volume (that is, the 3-dimensional region occupied by the volume after accounting for <see cref="VoxelSpacing"/>).
		/// </summary>
		Rectangle3D VolumeBounds { get; }

		/// <summary>
		/// Gets the spacing between voxels in millimetres (mm).
		/// </summary>
		/// <remarks>
		/// Equivalently, this is the size of a single voxel in millimetres along each dimension, and is the volumetric analogue of an image's Pixel Spacing.
		/// </remarks>
		Vector3D VoxelSpacing { get; }

		/// <summary>
		/// Gets the origin of the volume in the patient coordinate system.
		/// </summary>
		/// <remarks>
		/// This is the volumetric analogue of the Image Position (Patient) concept in DICOM.
		/// </remarks>
		Vector3D VolumePositionPatient { get; }

		/// <summary>
		/// Gets the direction of the volume X-axis in the patient coordinate system.
		/// </summary>
		/// <remarks>
		/// This is the volumetric analogue of the Image Orientation (Patient) concept in DICOM.
		/// </remarks>
		Vector3D VolumeOrientationPatientX { get; }

		/// <summary>
		/// Gets the direction of the volume Y-axis in the patient coordinate system.
		/// </summary>
		/// <remarks>
		/// This is the volumetric analogue of the Image Orientation (Patient) concept in DICOM.
		/// </remarks>
		Vector3D VolumeOrientationPatientY { get; }

		/// <summary>
		/// Gets the direction of the volume Z-axis in the patient coordinate system.
		/// </summary>
		/// <remarks>
		/// This is the volumetric analogue of the Image Orientation (Patient) concept in DICOM.
		/// </remarks>
		Vector3D VolumeOrientationPatientZ { get; }

		/// <summary>
		/// Gets the centre of the volume in the volume coordinate system.
		/// </summary>
		Vector3D VolumeCenter { get; }

		/// <summary>
		/// Gets the centre of the volume in the patient coordinate system.
		/// </summary>
		Vector3D VolumeCenterPatient { get; }

		/// <summary>
		/// Gets the value used for padding empty regions of the volume.
		/// </summary>
		int PaddingValue { get; }

		/// <summary>
		/// Gets the Rescale Slope of the linear modality LUT used to transform the values of the volume.
		/// </summary>
		/// <remarks>
		/// If the source frames have different rescale function parameters per frame, the volume data
		/// will be normalized such that they are defined by this one single rescale function.
		/// </remarks>
		double RescaleSlope { get; }

		/// <summary>
		/// Gets the Rescale Intercept of the linear modality LUT used to transform the values of the volume.
		/// </summary>
		/// <remarks>
		/// If the source frames have different rescale function parameters per frame, the volume data
		/// will be normalized such that they are defined by this one single rescale function.
		/// </remarks>
		double RescaleIntercept { get; }

		/// <summary>
		/// Gets the Modality of the source images from which the volume was created.
		/// </summary>
		string Modality { get; }

		/// <summary>
		/// Gets the Study Instance UID that identifies the source images from which the volume was created.
		/// </summary>
		string SourceStudyInstanceUid { get; }

		/// <summary>
		/// Gets the Series Instance UID that identifies the source images from which the volume was created.
		/// </summary>
		string SourceSeriesInstanceUid { get; }

		/// <summary>
		/// Gets the Frame of Reference UID that correlates the patient coordinate system with other data sources.
		/// </summary>
		string FrameOfReferenceUid { get; }

		/// <summary>
		/// Gets the DICOM data set containing the common values in the source images from which the volume was created.
		/// </summary>
		/// <remarks>
		/// In general, this data set should be considered read only. This is especially true if you are using the <see cref="Volume"/>
		/// in conjunction with the <see cref="VolumeCache"/>, as any changes are only temporary and will be lost if the volume
		/// instance were unloaded by the memory manager and subsequently reloaded on demand.
		/// </remarks>
		IVolumeDataSet DataSet { get; }

		/// <summary>
		/// Converts the specified volume position into the patient coordinate system.
		/// </summary>
		Vector3D ConvertToPatient(Vector3D volumePosition);

		/// <summary>
		/// Converts the specified patient position into the volume coordinate system.
		/// </summary>
		Vector3D ConvertToVolume(Vector3D patientPosition);

		/// <summary>
		/// Rotates the specified volume orientation matrix into the patient coordinate system.
		/// </summary>
		Matrix RotateToPatientOrientation(Matrix volumeOrientation);

		/// <summary>
		/// Rotates the specified patient orientation matrix into the volume coordinate system.
		/// </summary>
		Matrix RotateToVolumeOrientation(Matrix patientOrientation);

		/// <summary>
		/// Rotates the specified volume vector into the patient coordinate system.
		/// </summary>
		Vector3D RotateToPatientOrientation(Vector3D volumeVector);

		/// <summary>
		/// Rotates the specified patient vector into the volume coordinate system.
		/// </summary>
		Vector3D RotateToVolumeOrientation(Vector3D patientVector);
	}

	/// <summary>
	/// Base implementation of <see cref="IVolumeHeader"/>.
	/// </summary>
	internal abstract class VolumeHeaderBase : IVolumeHeader
	{
		protected abstract VolumeHeaderData VolumeHeaderData { get; }

		public Size3D ArrayDimensions
		{
			get { return VolumeHeaderData.ArrayDimensions; }
		}

		public Vector3D VolumeSize
		{
			get { return VolumeHeaderData.VolumeSize; }
		}

		public Rectangle3D VolumeBounds
		{
			get { return VolumeHeaderData.VolumeBounds; }
		}

		public Vector3D VoxelSpacing
		{
			get { return VolumeHeaderData.VoxelSpacing; }
		}

		public Vector3D VolumePositionPatient
		{
			get { return VolumeHeaderData.VolumePositionPatient; }
		}

		public Vector3D VolumeOrientationPatientX
		{
			get { return VolumeHeaderData.VolumeOrientationPatientX; }
		}

		public Vector3D VolumeOrientationPatientY
		{
			get { return VolumeHeaderData.VolumeOrientationPatientY; }
		}

		public Vector3D VolumeOrientationPatientZ
		{
			get { return VolumeHeaderData.VolumeOrientationPatientZ; }
		}

		public Vector3D VolumeCenter
		{
			get { return VolumeHeaderData.VolumeCenter; }
		}

		public Vector3D VolumeCenterPatient
		{
			get { return VolumeHeaderData.VolumeCenterPatient; }
		}

		public int BitsPerVoxel
		{
			get { return VolumeHeaderData.BitsPerVoxel; }
		}

		public bool Signed
		{
			get { return VolumeHeaderData.Signed; }
		}

		public int PaddingValue
		{
			get { return VolumeHeaderData.PaddingValue; }
		}

		public string Modality
		{
			get { return VolumeHeaderData.Modality; }
		}

		public string SourceStudyInstanceUid
		{
			get { return VolumeHeaderData.SourceStudyInstanceUid; }
		}

		public string SourceSeriesInstanceUid
		{
			get { return VolumeHeaderData.SourceSeriesInstanceUid; }
		}

		public string FrameOfReferenceUid
		{
			get { return VolumeHeaderData.FrameOfReferenceUid; }
		}

		public double RescaleSlope
		{
			get { return VolumeHeaderData.RescaleSlope; }
		}

		public double RescaleIntercept
		{
			get { return VolumeHeaderData.RescaleIntercept; }
		}

		public IVolumeDataSet DataSet
		{
			get { return VolumeHeaderData; }
		}

		public Vector3D ConvertToPatient(Vector3D volumePosition)
		{
			return VolumeHeaderData.ConvertToPatient(volumePosition);
		}

		public Vector3D ConvertToVolume(Vector3D patientPosition)
		{
			return VolumeHeaderData.ConvertToVolume(patientPosition);
		}

		public Matrix RotateToPatientOrientation(Matrix volumeOrientation)
		{
			return VolumeHeaderData.RotateToPatientOrientation(volumeOrientation);
		}

		public Matrix RotateToVolumeOrientation(Matrix patientOrientation)
		{
			return VolumeHeaderData.RotateToVolumeOrientation(patientOrientation);
		}

		public Vector3D RotateToPatientOrientation(Vector3D volumeVector)
		{
			return VolumeHeaderData.RotateToPatientOrientation(volumeVector);
		}

		public Vector3D RotateToVolumeOrientation(Vector3D patientVector)
		{
			return VolumeHeaderData.RotateToVolumeOrientation(patientVector);
		}
	}
}