using Khorde.Entities.Authoring;
using System;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Entities.Test
{
	class TestBlobAuthoring : MonoBehaviour
	{
		[Serializable]
		struct Item
		{
			public GameObject prefab;
			public float weight;
		}

		[SerializeField]
		Item[] items = Array.Empty<Item>();

		[SerializeField]
		Material weakTexture;

		[SerializeField]
		Material strongTexture;

		class Baker : Baker<TestBlobAuthoring>
		{
			public override void Bake(TestBlobAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.None);
				var builder = new RichBlobBuilder<TestBlob>(this);

				var items = builder.Builder.Allocate(ref builder.Value.items, authoring.items.Length);
				for(int i = 0; i < authoring.items.Length; ++i)
					items[i].prefab.Write(GetEntity(authoring.items[i].prefab, TransformUsageFlags.None), builder);

				builder.Value.weakTexture.Write(authoring.weakTexture, builder);
				builder.Value.strongTexture.Write(authoring.strongTexture, builder);

				AddComponent(entity, new TestComponent
				{
					data = builder.CreateAndRegisterBlobAssetReference(),
				});

				SetComponentEnabled<TestComponent>(entity, true);
			}
		}
	}
}