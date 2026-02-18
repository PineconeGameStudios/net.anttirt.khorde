using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Khorde.Expr
{
	public static class NativeArrayExt
	{
	    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
	    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	    private static void CheckElementReadAccess<T>(ref NativeArray<T> array, int index) where T : unmanaged
	    {
	        if ((uint)index >= (uint)array.Length)
	            throw new IndexOutOfRangeException(
	                $"Index {index} is out of range (must be between 0 and {array.Length - 1}).");
	    }

	    public static unsafe ref T UnsafeElementAt<T>(ref this NativeArray<T> array, int index) where T : unmanaged
	    {
	        CheckElementReadAccess(ref array, index);
	        return ref ((T*)array.GetUnsafePtr())[index];
	    }
	}
}