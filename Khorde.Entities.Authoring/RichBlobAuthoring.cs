using Khorde.Blobs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEngine;

namespace Khorde.Entities.Authoring
{
	struct PendingObjectRefPatch
	{
		/// <summary>
		/// Pointer to an allocated field within a blob, to be passed to <see cref="BlobBuilder.SetPointer{T}(ref BlobPtr{T}, ref T)"/> as the second argument
		/// </summary>
		internal IntPtr BlobObjectRefPtr;

		/// <summary>
		/// The actual target object
		/// </summary>
		internal UntypedObjectRef Target;
	}

	struct PendingEntityPatch
	{
		/// <summary>
		/// Pointer to an allocated field within a blob, to be passed to <see cref="BlobBuilder.SetPointer{T}(ref BlobPtr{T}, ref T)"/> as the second argument
		/// </summary>
		internal IntPtr BlobEntityPtr;

		/// <summary>
		/// The actual target entity
		/// </summary>
		internal Entity Target;
	}

	public static class RichBlobAuthoringExt
	{
		/// <summary>
		/// Write a loadable asset reference value
		/// </summary>
		/// <typeparam name="TObject"></typeparam>
		/// <typeparam name="TBlob"></typeparam>
		/// <param name="self"></param>
		/// <param name="obj"></param>
		/// <param name="context"></param>
		public static void Write<TObject, TBlob>(ref this BlobWeakObjectReference<TObject> self, TObject obj, in RichBlobBuilder<TBlob> context) where TBlob : unmanaged
			where TObject : UnityEngine.Object
		{
			// the value is stable from editor to build, so it doesn't need to be patched
			if(context.Baker != null)
				context.Baker.DependsOn(obj);

			var objRef = new WeakObjectReference<TObject>(obj);
			self.Id = new(objRef.Id);
			context.WeakObjRefSet.Add(objRef.Id);
		}

		/// <summary>
		/// Write a loadable entity prefab reference
		/// </summary>
		/// <typeparam name="TBlob"></typeparam>
		/// <param name="self"></param>
		/// <param name="obj"></param>
		/// <param name="context"></param>
		public static void Write<TBlob>(ref this BlobEntityPrefabReference self, GameObject obj, in RichBlobBuilder<TBlob> context) where TBlob : unmanaged
		{
			// the value is stable from editor to build, so it doesn't need to be patched
			if(context.Baker != null)
				context.Baker.DependsOn(obj);

			var objRef = new EntityPrefabReference(obj);
			self = UnsafeUtility.As<EntityPrefabReference, BlobEntityPrefabReference>(ref objRef);
			context.WeakObjRefSet.Add(self.Id);
		}

		/// <summary>
		/// Write a loadable entity scene reference
		/// </summary>
		/// <typeparam name="TBlob"></typeparam>
		/// <param name="self"></param>
		/// <param name="obj"></param>
		/// <param name="context"></param>
		public static void Write<TBlob>(ref this BlobEntitySceneReference self, SceneAsset obj, in RichBlobBuilder<TBlob> context) where TBlob : unmanaged
		{
			// the value is stable from editor to build, so it doesn't need to be patched
			if(context.Baker != null)
				context.Baker.DependsOn(obj);

			var objRef = new EntitySceneReference(obj);
			self = UnsafeUtility.As<EntitySceneReference, BlobEntitySceneReference>(ref objRef);
			context.WeakObjRefSet.Add(self.Id);
		}

		/// <summary>
		/// Write a loadable gameobject scene reference
		/// </summary>
		/// <typeparam name="TBlob"></typeparam>
		/// <param name="self"></param>
		/// <param name="obj"></param>
		/// <param name="context"></param>
		public static void Write<TBlob>(ref this BlobObjectSceneReference self, SceneAsset obj, in RichBlobBuilder<TBlob> context) where TBlob : unmanaged
		{
			// the value is stable from editor to build, so it doesn't need to be patched
			if(context.Baker != null)
				context.Baker.DependsOn(obj);

			var uwr = UntypedWeakReferenceId.CreateFromObjectInstance(obj);
			self.Id = new(uwr);
			context.WeakObjRefSet.Add(self.Id);
		}

		/// <summary>
		/// Write an Entity value.
		/// </summary>
		/// <typeparam name="TBlob"></typeparam>
		/// <param name="entity"></param>
		/// <param name="context"></param>
		public static void Write<TBlob>(ref this BlobEntity self, Entity entity, in RichBlobBuilder<TBlob> context) where TBlob : unmanaged
		{
			if(context.Baker == null)
				throw new InvalidOperationException("Cannot bake strong entity references into a BlobAsset. Use a BlobEntityPrefabReference instead.");

			// the actual value will be patched at runtime after loading
			self = default;
			PendingEntityPatch patch = default;
			unsafe
			{
				patch.BlobEntityPtr = (IntPtr)UnsafeUtility.AddressOf(ref self);
			}

			patch.Target = entity;
			context.EntityPatches.Add(patch);
		}

		/// <summary>
		/// Write an auto-loaded asset reference value
		/// </summary>
		/// <typeparam name="TObject"></typeparam>
		/// <typeparam name="TBlob"></typeparam>
		/// <param name="self"></param>
		/// <param name="obj"></param>
		/// <param name="context"></param>
		public static void Write<TObject, TBlob>(ref this BlobObjectRef<TObject> self, TObject obj, in RichBlobBuilder<TBlob> context) where TBlob : unmanaged
			where TObject : UnityEngine.Object
		{
			// TODO: we could in fact support this
			// * add the strong references to a regular serialized list in the blob asset
			// * patch the instance ids at runtime when loading the asset
			if(context.Baker == null)
				throw new InvalidOperationException("Cannot bake strong object references into a BlobAsset. Use a BlobWeakObjectReference instead.");

			// the actual value will be patched at runtime after loading
			self = default;
			PendingObjectRefPatch patch = default;
			unsafe
			{
				patch.BlobObjectRefPtr = (IntPtr)UnsafeUtility.AddressOf(ref self);
			}

			context.Baker.DependsOn(obj);
			int instanceId = obj != null ? obj.GetInstanceID() : 0;
			patch.Target = UnsafeUtility.As<int, UntypedObjectRef>(ref instanceId);
			context.ObjRefPatches.Add(patch);
		}
	}

	/// <summary>
	/// Wrapper for <see cref="BlobBuilder"/> used for creating blobs that can
	/// contain asset and entity references.
	/// </summary>
	/// <typeparam name="TBlob"></typeparam>
	public unsafe ref struct RichBlobBuilder<TBlob> where TBlob : unmanaged
	{
		internal IBaker Baker;
		internal BlobAsset<RichBlob<TBlob>> BlobAsset;
		internal int SchemaVersion;
		internal Entity Entity;
		internal INativeList<RichBlobEntityHolder> Entities;
		internal INativeList<RichBlobReferenceHolder> ObjRefs;
		internal INativeList<RichBlobWeakReferenceHolder> WeakObjRefs;
		internal NativeList<PendingEntityPatch> EntityPatches;
		internal NativeList<PendingObjectRefPatch> ObjRefPatches;
		internal NativeHashSet<UntypedWeakReferenceId> WeakObjRefSet;
		internal RichBlob<TBlob>* RootPtr;

		public BlobBuilder Builder;

		public RichBlobBuilder(IBaker baker, int schemaVersion)
		{
			Baker = baker;
			BlobAsset = null;
			SchemaVersion = schemaVersion;

			// TODO: add a baking system to deduplicate these entities
			Entity = baker.CreateAdditionalEntity(TransformUsageFlags.None);
			Entities = baker.AddBuffer<RichBlobEntityHolder>(Entity);
			ObjRefs = baker.AddBuffer<RichBlobReferenceHolder>(Entity);
			WeakObjRefs = baker.AddBuffer<RichBlobWeakReferenceHolder>(Entity);
			EntityPatches = new NativeList<PendingEntityPatch>(Allocator.Temp);
			ObjRefPatches = new NativeList<PendingObjectRefPatch>(Allocator.Temp);
			WeakObjRefSet = new NativeHashSet<UntypedWeakReferenceId>(0, Allocator.Temp);
			Builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref Builder.ConstructRoot<RichBlob<TBlob>>();
			root.PatchData.PatchedWorldSequenceNumber = ~0ul;
			fixed(RichBlob<TBlob>* rootPtr = &root)
				RootPtr = rootPtr;
		}

		public RichBlobBuilder(BlobAsset<RichBlob<TBlob>> blobAsset, int schemaVersion)
		{
			Baker = null;
			BlobAsset = blobAsset;
			SchemaVersion = schemaVersion;

			Entity = default;
			Entities = new NativeList<RichBlobEntityHolder>(0, Allocator.Temp);
			ObjRefs = new NativeList<RichBlobReferenceHolder>(0, Allocator.Temp);
			WeakObjRefs = new NativeList<RichBlobWeakReferenceHolder>(0, Allocator.Temp);
			EntityPatches = new NativeList<PendingEntityPatch>(Allocator.Temp);
			ObjRefPatches = new NativeList<PendingObjectRefPatch>(Allocator.Temp);
			WeakObjRefSet = new NativeHashSet<UntypedWeakReferenceId>(0, Allocator.Temp);
			Builder = new BlobBuilder(Allocator.Temp);
			ref var root = ref Builder.ConstructRoot<RichBlob<TBlob>>();
			root.PatchData.PatchedWorldSequenceNumber = ~0ul;
			fixed(RichBlob<TBlob>* rootPtr = &root)
				RootPtr = rootPtr;
		}

		/// <summary>
		/// Get a reference to the blob data being built. Further allocations can be made using the <see cref="Builder"/> field.
		/// </summary>
		public ref TBlob Value
		{
			get
			{
				return ref RootPtr->InnerData;
			}
		}

		/// <summary>
		/// Create a blob asset reference and register it with the baker. The returned reference can be stored directly in a component.
		/// </summary>
		/// <returns></returns>
		public BlobAssetReference<RichBlob<TBlob>> CreateAndRegisterBlobAssetReference()
		{
			var entityPatches = Builder.Allocate(ref RootPtr->PatchData.EntityPatches, EntityPatches.Length);
			var entityBufferIndices = new NativeHashMap<Entity, int>(EntityPatches.Length, Allocator.Temp);

			for(int i = 0; i < EntityPatches.Length; ++i)
			{
				var patch = EntityPatches[i];

				if(!entityBufferIndices.TryGetValue(patch.Target, out var index))
				{
					index = Entities.Length;
					entityBufferIndices.Add(patch.Target, index);
					Entities.Length += 1;
					Entities[Entities.Length - 1] = new RichBlobEntityHolder { Value = patch.Target };
				}

				Builder.SetPointer(ref entityPatches[i].PatchLocation, ref UnsafeUtility.AsRef<Entity>((void*)patch.BlobEntityPtr));
			}

			var objRefPatches = Builder.Allocate(ref RootPtr->PatchData.ObjRefPatches, ObjRefPatches.Length);
			var objRefBufferIndices = new NativeHashMap<UntypedObjectRef, int>(ObjRefPatches.Length, Allocator.Temp);

			for(int i = 0; i < ObjRefPatches.Length; ++i)
			{
				var patch = ObjRefPatches[i];

				if(!objRefBufferIndices.TryGetValue(patch.Target, out var index))
				{
					index = ObjRefs.Length;
					objRefBufferIndices.Add(patch.Target, index);
					ObjRefs.Length += 1;
					ObjRefs[ObjRefs.Length - 1] = new RichBlobReferenceHolder { Value = UnsafeUtility.As<UntypedObjectRef, UnityObjectRef<UnityEngine.Object>>(ref patch.Target) };
				}

				Builder.SetPointer(ref objRefPatches[i].PatchLocation, ref UnsafeUtility.AsRef<UntypedObjectRef>((void*)patch.BlobObjectRefPtr));
			}

			foreach(var weakRef in WeakObjRefSet)
			{
				// these don't need to be patched as the weak ids are already stable
				WeakObjRefs.Length += 1;
				WeakObjRefs[WeakObjRefs.Length - 1] = new RichBlobWeakReferenceHolder { Id = weakRef };
			}

			if(Baker != null)
			{
				var assetRef = Builder.CreateBlobAssetReference<RichBlob<TBlob>>(Allocator.Persistent);
				Baker.AddBlobAsset(ref assetRef, out _);
				Baker.AddComponent(Entity, new PatchableRichBlob { Asset = UnsafeUntypedBlobAssetReference.Create(assetRef) });
				return assetRef;
			}
			else
			{
				var weakRefs = new List<UntypedWeakReferenceId>(WeakObjRefs.Length);
				for(int i = 0; i < WeakObjRefs.Length; i++)
					weakRefs.Add(WeakObjRefs[i].Id);
				BlobAsset.SetAssetData(Builder, SchemaVersion, weakRefs);
				BlobAsset.TryReadInPlace(SchemaVersion, out var result);
				return result.Reference;
			}
		}
	}
}