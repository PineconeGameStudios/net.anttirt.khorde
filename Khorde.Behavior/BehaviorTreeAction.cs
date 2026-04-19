using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Khorde.Behavior
{
	public abstract class BehaviorTreeAction : ScriptableObject
	{
		public abstract void Invoke(ref SystemState state, Entity entity, in BehaviorTreeInvocation call);
	}

	public struct BehaviorTreeActionRef : IBufferElementData
	{
		public UnityObjectRef<BehaviorTreeAction> value;
	}

	[Serializable]
	public struct BehaviorTreeActionParam<T> where T : unmanaged
	{
		public ushort offset;
	}

	[InternalBufferCapacity(0)]
	public struct BehaviorTreeInvocation : IBufferElementData, IEnableableComponent
	{
		public Storage parameters;
		public int actionIndex;

		public const int ParamStorageSize = 128 - 16;

		public bool TryGet<T>(BehaviorTreeActionParam<T> param, out T value) where T : unmanaged
		{
			int size = UnsafeUtility.SizeOf<T>();

			if(param.offset + size > ParamStorageSize)
			{
				value = default;
				return false;
			}

			unsafe
			{
				fixed(Storage* pparams = &parameters)
				fixed(T* pvalue = &value)
				{
					byte* src = (byte*)pparams;
					byte* dst = (byte*)pvalue;

					UnsafeUtility.MemCpy(dst, src + param.offset, size);
					return true;
				}
			}
		}

		public T Get<T>(BehaviorTreeActionParam<T> param, T @default = default) where T : unmanaged
		{
			if(TryGet(param, out var value))
				return value;

			return @default;
		}

		public unsafe NativeArray<byte> UnsafeGetTempStorageArray()
		{
			fixed(Storage* pstorage = &parameters)
			{
				var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(pstorage, UnsafeUtility.SizeOf<Storage>(), Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
				return result;
			}
		}

		public struct Storage
		{
			public int4
				i0, i1, i2, i3,
				i4, i5, i6;
		}
	}
}