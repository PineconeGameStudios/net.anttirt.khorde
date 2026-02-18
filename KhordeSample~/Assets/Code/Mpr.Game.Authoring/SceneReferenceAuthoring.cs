using System;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEditor;
using UnityEngine;

namespace Mpr.Game.Authoring
{
	class SceneReferenceAuthoring : MonoBehaviour
	{
		public WeakObjectSceneReference[] scenes = Array.Empty<WeakObjectSceneReference>();

		class Baker : Baker<SceneReferenceAuthoring>
		{
			public override void Bake(SceneReferenceAuthoring authoring)
			{
				var entity = GetEntity(authoring, TransformUsageFlags.None);
				var references = AddBuffer<SceneReference>(entity);
				foreach(var scene in authoring.scenes)
					if(scene.IsReferenceValid)
						references.Add(new SceneReference { reference = scene });
			}
		}
	}
}