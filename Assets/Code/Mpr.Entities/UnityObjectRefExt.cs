using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Mpr.Entities;

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
}