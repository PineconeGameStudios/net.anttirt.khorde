using Unity.Entities;
using UnityEngine;

namespace Mpr.Entities.Test
{
	struct TestItem
	{
		public BlobEntity prefab;
		public float weight;
	}

	struct TestBlob
	{
		public BlobArray<TestItem> items;
		public BlobWeakObjectReference<Material> weakTexture;
		public BlobObjectRef<Material> strongTexture;
	}

	struct TestComponent : IComponentData, IEnableableComponent
	{
		public BlobAssetReference<RichBlob<TestBlob>> data;
		public bool itemsSpawned;
		public bool weakTextureLoadStarted;
		public bool strongTextureLogged;
		public bool patchWorldChecked;
	}

	partial struct TestComponentSystem : ISystem
	{
		void ISystem.OnUpdate(ref SystemState state)
		{
			var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

			foreach(var (tcRef, entity) in SystemAPI.Query<RefRW<TestComponent>>().WithEntityAccess())
			{
				ref var tc = ref tcRef.ValueRW;

				if(!tc.patchWorldChecked)
				{
					if(!tc.data.Value.IsPatched(state.WorldUnmanaged))
					{
						Debug.LogError($"patched for the wrong world: expected {state.WorldUnmanaged.SequenceNumber} but got {tc.data.Value.PatchedWorldSequenceNumber}");
					}
					else
					{
						Debug.Log($"patched for the world with sequence {state.WorldUnmanaged.SequenceNumber}");
					}

					tc.patchWorldChecked = true;
				}

				ref var blob = ref tc.data.Value.Value;

				if(!tc.itemsSpawned)
				{
					for(int i = 0; i < blob.items.Length; ++i)
					{
						if(blob.items[i].prefab.AsEntity != Entity.Null)
						{
							ecb.Instantiate(blob.items[i].prefab.AsEntity);
							Debug.Log("entity prefab instantiated");
						}
						else
						{
							Debug.Log("entity prefab was null");
						}
					}

					tc.itemsSpawned = true;
				}

				if(!tc.strongTextureLogged)
				{
					Debug.Log($"strong texture ref: {blob.strongTexture.Value}");
					tc.strongTextureLogged = true;
				}

				if(blob.weakTexture.LoadingStatus == Unity.Entities.Content.ObjectLoadingStatus.None && !tc.weakTextureLoadStarted)
				{
					Debug.Log($"weak texture load started");
					blob.weakTexture.LoadAsync();

					tc.weakTextureLoadStarted = true;
				}

				var status = blob.weakTexture.LoadingStatus;
				switch(status)
				{
					case Unity.Entities.Content.ObjectLoadingStatus.None:
						Debug.Log($"weak texture load not started");
						state.EntityManager.SetComponentEnabled<TestComponent>(entity, false);
						break;

					case Unity.Entities.Content.ObjectLoadingStatus.Queued:
					case Unity.Entities.Content.ObjectLoadingStatus.Loading:
						break;

					case Unity.Entities.Content.ObjectLoadingStatus.Completed:
						Debug.Log($"weak texture loaded: {blob.weakTexture.Result}");
						blob.weakTexture.Release();
						state.EntityManager.SetComponentEnabled<TestComponent>(entity, false);
						break;

					case Unity.Entities.Content.ObjectLoadingStatus.Error:
						Debug.Log($"weak texture load failed");
						state.EntityManager.SetComponentEnabled<TestComponent>(entity, false);
						break;

					default:
						break;
				}
			}
		}
	}
}