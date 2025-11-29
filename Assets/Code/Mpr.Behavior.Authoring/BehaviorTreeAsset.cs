using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Mpr.Behavior
{
	public class BehaviorTreeAsset : ScriptableObject
	{
		public TextAsset bakedGraph;

		public BlobAssetReference<BTData> LoadPersistent()
		{
			var data = bakedGraph.GetData<byte>();
			unsafe
			{
				var reader = new MemoryBinaryReader((byte*)data.GetUnsafePtr(), data.Length);
				if(!BlobAssetReference<BTData>.TryRead(reader, 0, out var result))
					throw new System.Exception("failed to load behavior tree blob from imported binary asset");
				return result;
			}
		}
	}
}