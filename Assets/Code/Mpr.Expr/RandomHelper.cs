using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Mpr.Expr;

public static class RandomHelper
{
	// keep every per-thread random object on its own cache line to avoid false sharing
	[StructLayout(LayoutKind.Explicit, Size = 64)]
	struct FalseSharingRandomContainer
	{
		[FieldOffset(0)]
		public Unity.Mathematics.Random random;
	}

	static readonly SharedStatic<NativeArray<FalseSharingRandomContainer>> Data =
		SharedStatic<NativeArray<FalseSharingRandomContainer>>.GetOrCreate<FalseSharingRandomContainer>();

	const int MaxThreads = 32;

	public static ref Unity.Mathematics.Random JobRandom
	{
		get
		{
			return ref Data.Data.UnsafeElementAt(JobsUtility.ThreadIndex % MaxThreads).random;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoadMethod]
#endif
	static void Initialize()
	{
		Data.Data = new NativeArray<FalseSharingRandomContainer>(MaxThreads, Allocator.Domain);
		long seed = System.Diagnostics.Stopwatch.GetTimestamp();
		for(int i = 0; i < MaxThreads; ++i)
		{
			var hash = UnityEngine.Hash128.Compute(seed + i);
			Data.Data.UnsafeElementAt(i).random = new Unity.Mathematics.Random(((Unity.Entities.Hash128)hash).Value.x);
		}
	}
}
