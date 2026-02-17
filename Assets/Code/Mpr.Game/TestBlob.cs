using TMPro;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Entities.Test
{
	public struct TestItem
	{
		public BlobEntity prefab;
		public float weight;
	}

	public struct TestBlob
	{
		public BlobArray<TestItem> items;
		public BlobWeakObjectReference<Material> weakTexture;
		public BlobObjectRef<Material> strongTexture;
	}

	public struct TestComponent : IComponentData, IEnableableComponent
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
			var text = GameObject.FindFirstObjectByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude);
			if(text == null)
				return;

			var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

			foreach(var (tcRef, entity) in SystemAPI.Query<RefRW<TestComponent>>().WithEntityAccess())
			{
				ref var tc = ref tcRef.ValueRW;

				if(!tc.patchWorldChecked)
				{
					if(!tc.data.Value.IsPatched(state.WorldUnmanaged))
					{
						Debug.LogError($"patched for the wrong world: expected {state.WorldUnmanaged.SequenceNumber} but got {tc.data.Value.PatchedWorldSequenceNumber}");
						text.text += ($"patched for the wrong world: expected {state.WorldUnmanaged.SequenceNumber} but got {tc.data.Value.PatchedWorldSequenceNumber}\n");
					}
					else
					{
						Debug.Log($"patched for the world with sequence {state.WorldUnmanaged.SequenceNumber}");
						text.text += ($"patched for the world with sequence {state.WorldUnmanaged.SequenceNumber}\n");
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
							text.text += ("entity prefab instantiated\n");
						}
						else
						{
							Debug.Log("entity prefab was null");
							text.text += ("entity prefab was null\n");
						}
					}

					tc.itemsSpawned = true;
				}

				if(!tc.strongTextureLogged)
				{
					Debug.Log($"strong texture ref: {blob.strongTexture.Value}");
					text.text += ($"strong texture ref: {blob.strongTexture.Value}\n");
					tc.strongTextureLogged = true;
				}

				if(blob.weakTexture.LoadingStatus == Unity.Entities.Content.ObjectLoadingStatus.None && !tc.weakTextureLoadStarted)
				{
					Debug.Log($"weak texture load started");
					text.text += ($"weak texture load started\n");
					blob.weakTexture.LoadAsync();

					tc.weakTextureLoadStarted = true;
				}

				var status = blob.weakTexture.LoadingStatus;
				switch(status)
				{
					case Unity.Entities.Content.ObjectLoadingStatus.None:
						Debug.Log($"weak texture load not started");
						text.text += ($"weak texture load not started\n");
						state.EntityManager.SetComponentEnabled<TestComponent>(entity, false);
						break;

					case Unity.Entities.Content.ObjectLoadingStatus.Queued:
					case Unity.Entities.Content.ObjectLoadingStatus.Loading:
						break;

					case Unity.Entities.Content.ObjectLoadingStatus.Completed:
						Debug.Log($"weak texture loaded: {blob.weakTexture.Result}");
						text.text += ($"weak texture loaded: {blob.weakTexture.Result}\n");
						blob.weakTexture.Release();
						state.EntityManager.SetComponentEnabled<TestComponent>(entity, false);
						break;

					case Unity.Entities.Content.ObjectLoadingStatus.Error:
						Debug.Log($"weak texture load failed");
						text.text += ($"weak texture load failed\n");
						state.EntityManager.SetComponentEnabled<TestComponent>(entity, false);
						break;

					default:
						break;
				}
			}
		}
	}
}