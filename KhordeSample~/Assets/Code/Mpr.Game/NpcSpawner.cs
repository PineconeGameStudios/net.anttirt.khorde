using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Mpr.Game
{

	[Serializable]
	public struct NpcSpawnerConfig : IComponentData
	{
		public int2 gridSize;
		public float gridSpacing;
		public float3 origin;
		public bool planar;
		[NonSerialized]
		public Entity prefab;
	}

}