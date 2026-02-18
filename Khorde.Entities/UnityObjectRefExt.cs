using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

#if UNITY_6000_4_OR_NEWER
#error TODO: fix when UnityObjectRef switches to EntityId
using UnityObjectRefId = UnityEngine.EntityId;
#else
using UnityObjectRefId = System.Int32;
#endif

namespace Khorde.Entities
{
	public static class UnityObjectRefExt
	{
	    /// <summary>
	    /// Safely convert a <see cref="UnityObjectRef{T}"/> to a reference to a base class
	    /// </summary>
	    /// <param name="reference"></param>
	    /// <typeparam name="TDerived"></typeparam>
	    /// <typeparam name="TBase"></typeparam>
	    /// <returns></returns>
	    public static UnityObjectRef<TBase> UpCast<TDerived, TBase>(this UnityObjectRef<TDerived> reference) where TDerived : TBase where TBase : UnityEngine.Object
	    {
	        return UnsafeUtility.As<UnityObjectRef<TDerived>, UnityObjectRef<TBase>>(ref reference);
	    }

	    /// <summary>
	    /// Get the underlying object instance id
	    /// </summary>
	    /// <param name="reference"></param>
	    /// <typeparam name="T"></typeparam>
	    /// <returns></returns>
	    public static UnityObjectRefId GetObjectId<T>(this UnityObjectRef<T> reference) where T : UnityEngine.Object
	    {
	        return UnsafeUtility.As<UnityObjectRef<T>, UnityObjectRefId>(ref reference);
	    }
	}
}