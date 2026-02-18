using Khorde.Expr.Authoring;
using System;
using Unity.Entities;
using UnityEngine;

namespace Khorde.Query.Test
{
	public class TestPlayerControllerAuthoring : MonoBehaviour
	{
		public TestPlayerController controller;

		class Baker : Baker<TestPlayerControllerAuthoring>
		{
			public override void Bake(TestPlayerControllerAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
				AddComponent(entity, authoring.controller);
				//AddComponent(entity, new PlayerInput
				//{

				//});
			}
		}
	}

	[Serializable] class ReadTestPlayerController : ComponentReaderNode<TestPlayerController> { }
	//[Serializable] class WritePlayerController : ComponentWriterNode<TestPlayerController> { }
}
