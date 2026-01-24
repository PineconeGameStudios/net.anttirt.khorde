using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Entities.Serialization;

#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Mpr.Entities.Authoring")]
#endif

namespace Mpr.Entities
{
	// Workaround to be able to store UntypedWeakReferenceId in a blob.
	// Usually we want to issue an error when storing UntypedWeakReferenceId in a blob to alert users that this is not yet supported
	// But in this specific case we know what we are doing. This type must be binary compatible with  UntypedWeakReferenceId
	internal struct UnsafeUntypedWeakReferenceId
	{
		public UnsafeUntypedWeakReferenceId(UntypedWeakReferenceId weakAssetRef)
		{
			GlobalId = weakAssetRef.GlobalId;
			GenerationType = weakAssetRef.GenerationType;
		}
		public RuntimeGlobalObjectId GlobalId;
		public WeakReferenceGenerationType GenerationType;
	}

	internal struct UntypedObjectRef : IEquatable<UntypedObjectRef>
	{
		// currently a InstanceId, later switch this EntityId repr
		internal int Value;

		public bool Equals(UntypedObjectRef other) => Value == other.Value;
	}

	/// <summary>
	/// Store a weak reference to an asset inside a blob.
	/// </summary>
	/// <typeparam name="TObject"></typeparam>
	[MayOnlyLiveInBlobStorage]
	public struct BlobWeakObjectReference<TObject>
		where TObject : UnityEngine.Object
	{
		internal UnsafeUntypedWeakReferenceId Id;

		public WeakObjectReference<TObject> AsWeakObjectReference => UnsafeUtility.As<BlobWeakObjectReference<TObject>, WeakObjectReference<TObject>>(ref this);

		public ObjectLoadingStatus LoadingStatus => AsWeakObjectReference.LoadingStatus;

		public TObject Result => AsWeakObjectReference.Result;

		public void LoadAsync() => AsWeakObjectReference.LoadAsync();

		public void Release() => AsWeakObjectReference.Release();
	}

	/// <summary>
	/// Store a reference to an entity inside a blob. Must be patched at runtime by calling <see cref="RichBlob{TBlob}.Patch"/>.
	/// </summary>
	[MayOnlyLiveInBlobStorage]
	public struct BlobEntity
	{
		internal int Index;
		internal int Version;

		public Entity AsEntity => UnsafeUtility.As<BlobEntity, Entity>(ref this);
	}

	/// <summary>
	/// Store a strong object reference inside a blob. Must be patched at runtime by calling <see cref="RichBlob{TBlob}.Patch"/>.
	/// </summary>
	/// <typeparam name="TObject"></typeparam>
	[MayOnlyLiveInBlobStorage]
	public struct BlobObjectRef<TObject> where TObject : UnityEngine.Object
	{
		internal UntypedObjectRef Id;

		public UnityObjectRef<TObject> AsObjectRef => UnsafeUtility.As<BlobObjectRef<TObject>, UnityObjectRef<TObject>>(ref this);

		public TObject Value => AsObjectRef.Value;
	}

	// these references are automatically picked up by the scene baking infrastructure
	// see EntityRemapUtility.CalculateOffsetsRecurse
	internal struct RichBlobEntityHolder : IBufferElementData { internal Entity Value; }
	internal struct RichBlobReferenceHolder : IBufferElementData { internal UnityObjectRef<UnityEngine.Object> Value; }
	internal struct RichBlobWeakReferenceHolder : IBufferElementData { internal UntypedWeakReferenceId Id; }
	internal struct PatchableRichBlob : IComponentData { internal UnsafeUntypedBlobAssetReference Asset; }

	internal struct BlobObjRefPatch
	{
		/// <summary>
		/// Reference to a <see cref="BlobObjectRef{TObject}"/>
		/// </summary>
		internal BlobPtr<UntypedObjectRef> PatchLocation;

		/// <summary>
		/// Index into <see cref="RichBlobReferenceHolder"/>
		/// </summary>
		internal int BufferIndex;
	}

	internal struct BlobEntityPatch
	{
		/// <summary>
		/// Reference to a <see cref="BlobEntity"/>
		/// </summary>
		internal BlobPtr<Entity> PatchLocation;

		/// <summary>
		/// Index into <see cref="RichBlobEntityHolder"/>
		/// </summary>
		internal int BufferIndex;
	}

	[MayOnlyLiveInBlobStorage]
	internal struct UntypedRichBlobPatchData
	{
		internal BlobArray<BlobObjRefPatch> ObjRefPatches;
		internal BlobArray<BlobEntityPatch> EntityPatches;
		internal ulong PatchedWorldSequenceNumber;

		/// <summary>
		/// Patch the rich blob after loading at runtime
		/// </summary>
		/// <param name="entities"></param>
		/// <param name="objRefs"></param>
		/// <param name="worldSequenceNumber"><see cref="WorldUnmanaged.SequenceNumber"/> for the current World</param>
		internal void Patch(DynamicBuffer<RichBlobEntityHolder> entities, DynamicBuffer<RichBlobReferenceHolder> objRefs, ulong worldSequenceNumber)
		{
			// the asset may already have been patched
			if(PatchedWorldSequenceNumber == worldSequenceNumber)
				return;

			for(int i = 0; i < EntityPatches.Length; ++i)
			{
				ref var patch = ref EntityPatches[i];
				patch.PatchLocation.Value = entities[patch.BufferIndex].Value;
			}

			for(int i = 0; i < ObjRefPatches.Length; ++i)
			{
				ref var patch = ref ObjRefPatches[i];
				patch.PatchLocation.Value = UnsafeUtility.As<UnityObjectRef<UnityEngine.Object>, UntypedObjectRef>(ref objRefs.ElementAt(patch.BufferIndex).Value);
			}

			PatchedWorldSequenceNumber = worldSequenceNumber;
		}
	}

	/// <summary>
	/// Wrapper for a blob that contains fields of type <see
	/// cref="BlobEntity"/>, <see cref="BlobObjectRef{TObject}"/> and/or <see
	/// cref="BlobWeakObjectReference{TObject}"/>. Must be created in a baker
	/// using <see cref="Mpr.Entities.Authoring.RichBlobBuilder{T}"/>.
	/// </summary>
	/// <typeparam name="TBlob">Contained blob type</typeparam>
	/// <remarks>
	/// Blob assets cannot contain references to external assets, to other
	/// blobs, etc, because the built-in reflection-based runtime patching
	/// system (<see cref="EntityRemapUtility"/>) cannot access the insides
	/// of blob assets, and can only look at fields stored on components.
	/// <para/>
	/// In order to enable this, we store all the required references
	/// in dynamic buffers along with the blob asset, and patch the blob
	/// asset manually at runtime after loading. This is performed by
	/// <see cref="PatchRichBlobSystem"/>. Once patched, the references
	/// can be accessed directly by just inspecting the blob data without
	/// having to pass around references to lookup buffers.
	/// </remarks>
	[MayOnlyLiveInBlobStorage]
	public struct RichBlob<TBlob> where TBlob : unmanaged
	{
		internal UntypedRichBlobPatchData PatchData;
		internal TBlob InnerData;

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void CheckPatched()
		{
			if(PatchData.PatchedWorldSequenceNumber == ~0u)
				throw new InvalidOperationException("RichBlob asset has not been patched.");
		}

		/// <summary>
		/// Get a reference to the stored blob value
		/// </summary>
		public unsafe ref TBlob Value
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				CheckPatched();

				// NOTE: returning by ref is ok since we're necessarily in blob storage
				fixed(TBlob* ptr = &InnerData)
					return ref *ptr;
			}
		}

		/// <summary>
		/// Check if the blob has been patched for the current world
		/// </summary>
		/// <param name="worldUnmanaged"></param>
		/// <returns></returns>
		public bool IsPatched(WorldUnmanaged worldUnmanaged) => PatchData.PatchedWorldSequenceNumber == worldUnmanaged.SequenceNumber;

		public ulong PatchedWorldSequenceNumber => PatchData.PatchedWorldSequenceNumber;
	}
}
