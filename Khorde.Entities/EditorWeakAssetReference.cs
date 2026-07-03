using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Khorde.Entities
{
	/// <summary>
	/// Used to store editor-only object references without including them in builds.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[Serializable]
	public struct EditorWeakAssetReference<T> where T : UnityEngine.Object
	{
		/// <summary>
		/// This has the same structure as <see
		/// cref="Unity.Entities.Content.WeakObjectReference{T}"/> but the
		/// referenced asset doesn't get pulled into builds. We reuse the
		/// structure here to be able to reuse the same PropertyDrawer
		/// implementation.
		/// </summary>
		[Serializable]
		struct Wrapper
		{
			public RuntimeGlobalObjectId GlobalId;
			public WeakReferenceGenerationType GenerationType;
		}

		[SerializeField]
		Wrapper Id;

#if UNITY_EDITOR
		public static implicit operator EditorWeakAssetReference<T>(T obj)
		{
			var id = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(obj);
			return new EditorWeakAssetReference<T>
			{
				Id = new() { GlobalId = UnsafeUtility.As<UnityEditor.GlobalObjectId, RuntimeGlobalObjectId>(ref id) },
			};
		}

		public T EditorGetValue()
		{
			var id = UnsafeUtility.As<RuntimeGlobalObjectId, UnityEditor.GlobalObjectId>(ref Id.GlobalId);
			return (T)UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
		}
#endif
	}

}