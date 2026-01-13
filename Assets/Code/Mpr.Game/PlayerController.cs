using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Mpr.Game
{
	[Serializable]
	public struct PlayerController : IComponentData
	{
		public float speed;
		public enum UpAxis { X, Y, Z, }
		public UpAxis up;
		public UnityObjectRef<Transform> cameraTarget;
	}

	public struct PlayerInput : IInputComponentData
	{
		public float2 move;
		public float rotate;
	}
}
