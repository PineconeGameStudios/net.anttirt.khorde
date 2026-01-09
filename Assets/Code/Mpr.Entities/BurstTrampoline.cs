using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Mpr.Entities;

internal delegate void BurstTrampolineInvoker0(IntPtr context);
internal delegate void BurstTrampolineInvoker1(IntPtr context, IntPtr dataPtr0);
internal delegate void BurstTrampolineInvoker2(IntPtr context, IntPtr dataPtr0, IntPtr dataPtr1);
internal delegate void BurstTrampolineInvoker3(IntPtr context, IntPtr dataPtr0, IntPtr dataPtr1, IntPtr dataPtr2);

public readonly struct BurstTrampoline
{
    private static readonly FunctionPointer<BurstTrampolineInvoker0> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker0> functionPointer;
    private readonly IntPtr context;
    public delegate void Delegate();
    public bool IsCreated => functionPointer.IsCreated;
    public void Invoke() => functionPointer.Invoke(context);

    public BurstTrampoline(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker0> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker0 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker0))]
    private static void Trampoline(IntPtr context) => ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke();
}

public readonly unsafe struct BurstTrampoline<T0> where T0 : unmanaged
{
    private static readonly FunctionPointer<BurstTrampolineInvoker1> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker1> functionPointer;
    private readonly IntPtr context;
    public delegate void Delegate(in T0 data);
    public bool IsCreated => functionPointer.IsCreated;

    public void Invoke(in T0 data)
    {
        fixed(T0* dataPtr = &data)
            functionPointer.Invoke(context, (IntPtr)dataPtr);  
    }

    public BurstTrampoline(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker1> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker1 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker1))]
    private static void Trampoline(IntPtr context, IntPtr dataPtr0)
    {
        ref var value = ref UnsafeUtility.AsRef<T0>((void*)dataPtr0);
        ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke(in value); 
    } 
}

public readonly unsafe struct BurstTrampoline<T0, T1>
    where T0 : unmanaged
    where T1 : unmanaged
{
    private static readonly FunctionPointer<BurstTrampolineInvoker2> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker2> functionPointer;
    private readonly IntPtr context;
    
    public delegate void Delegate(in T0 data0, in T1 data1);
    
    public bool IsCreated => functionPointer.IsCreated;

    public void Invoke(in T0 data0, in T1 data1)
    {
        fixed(T0* dataPtr0 = &data0)
        fixed(T1* dataPtr1 = &data1)
            functionPointer.Invoke(context, (IntPtr)dataPtr0, (IntPtr)dataPtr1);
    }

    public BurstTrampoline(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker2> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker2 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker2))]
    private static void Trampoline(IntPtr context, IntPtr dataPtr0, IntPtr dataPtr1)
    {
        ref var value0 = ref UnsafeUtility.AsRef<T0>((void*)dataPtr0);
        ref var value1 = ref UnsafeUtility.AsRef<T1>((void*)dataPtr1);
        ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke(in value0, in value1); 
    } 
}

public readonly unsafe struct BurstTrampoline<T0, T1, T2>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
{
    private static readonly FunctionPointer<BurstTrampolineInvoker3> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker3> functionPointer;
    private readonly IntPtr context;
    
    public delegate void Delegate(in T0 data0, in T1 data1, in T2 data2);
    
    public bool IsCreated => functionPointer.IsCreated;

    public void Invoke(in T0 data0, in T1 data1, in T2 data2)
    {
        fixed(T0* dataPtr0 = &data0)
        fixed(T1* dataPtr1 = &data1)
        fixed(T2* dataPtr2 = &data2)
            functionPointer.Invoke(context, (IntPtr)dataPtr0, (IntPtr)dataPtr1, (IntPtr)dataPtr2);
    }

    public BurstTrampoline(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker3> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker3 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker3))]
    private static void Trampoline(IntPtr context, IntPtr dataPtr0, IntPtr dataPtr1, IntPtr dataPtr2)
    {
        ref var value0 = ref UnsafeUtility.AsRef<T0>((void*)dataPtr0);
        ref var value1 = ref UnsafeUtility.AsRef<T1>((void*)dataPtr1);
        ref var value2 = ref UnsafeUtility.AsRef<T2>((void*)dataPtr2);
        ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke(in value0, in value1, in value2); 
    } 
}

public readonly unsafe struct BurstTrampolineOut<T0> where T0 : unmanaged
{
    private static readonly FunctionPointer<BurstTrampolineInvoker1> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker1> functionPointer;
    private readonly IntPtr context;
    public delegate void Delegate(out T0 data);
    public bool IsCreated => functionPointer.IsCreated;

    public void Invoke(out T0 data)
    {
        fixed(T0* dataPtr = &data)
            functionPointer.Invoke(context, (IntPtr)dataPtr);  
    }

    public BurstTrampolineOut(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker1> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker1 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker1))]
    private static void Trampoline(IntPtr context, IntPtr dataPtr0)
    {
        ref var value = ref UnsafeUtility.AsRef<T0>((void*)dataPtr0);
        ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke(out value); 
    } 
}

public readonly unsafe struct BurstTrampolineOut<T0, T1>
    where T0 : unmanaged
    where T1 : unmanaged
{
    private static readonly FunctionPointer<BurstTrampolineInvoker2> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker2> functionPointer;
    private readonly IntPtr context;
    
    public delegate void Delegate(in T0 data0, out T1 data1);
    
    public bool IsCreated => functionPointer.IsCreated;

    public void Invoke(in T0 data0, out T1 data1)
    {
        fixed(T0* dataPtr0 = &data0)
        fixed(T1* dataPtr1 = &data1)
            functionPointer.Invoke(context, (IntPtr)dataPtr0, (IntPtr)dataPtr1);
    }

    public BurstTrampolineOut(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker2> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker2 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker2))]
    private static void Trampoline(IntPtr context, IntPtr dataPtr0, IntPtr dataPtr1)
    {
        ref var value0 = ref UnsafeUtility.AsRef<T0>((void*)dataPtr0);
        ref var value1 = ref UnsafeUtility.AsRef<T1>((void*)dataPtr1);
        ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke(in value0, out value1); 
    } 
}

public readonly unsafe struct BurstTrampolineOut<T0, T1, T2>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
{
    private static readonly FunctionPointer<BurstTrampolineInvoker3> StaticInvoker = CreateInvokerFunctionPointer();
    private readonly FunctionPointer<BurstTrampolineInvoker3> functionPointer;
    private readonly IntPtr context;
    
    public delegate void Delegate(in T0 data0, in T1 data1, out T2 data2);
    
    public bool IsCreated => functionPointer.IsCreated;

    public void Invoke(in T0 data0, in T1 data1, out T2 data2)
    {
        fixed(T0* dataPtr0 = &data0)
        fixed(T1* dataPtr1 = &data1)
        fixed(T2* dataPtr2 = &data2)
            functionPointer.Invoke(context, (IntPtr)dataPtr0, (IntPtr)dataPtr1, (IntPtr)dataPtr2);
    }

    public BurstTrampolineOut(Delegate method)
    {
        functionPointer = StaticInvoker;
        context = GCHandle.ToIntPtr(GCHandle.Alloc(method));
    }

    private static FunctionPointer<BurstTrampolineInvoker3> CreateInvokerFunctionPointer()
    {
        BurstTrampolineInvoker3 invoker = Trampoline;
        GarbagePrevention.Objects.Add(invoker);
        return new(Marshal.GetFunctionPointerForDelegate(invoker));
    }

    [AOT.MonoPInvokeCallback(typeof(BurstTrampolineInvoker3))]
    private static void Trampoline(IntPtr context, IntPtr dataPtr0, IntPtr dataPtr1, IntPtr dataPtr2)
    {
        ref var value0 = ref UnsafeUtility.AsRef<T0>((void*)dataPtr0);
        ref var value1 = ref UnsafeUtility.AsRef<T1>((void*)dataPtr1);
        ref var value2 = ref UnsafeUtility.AsRef<T2>((void*)dataPtr2);
        ((Delegate)GCHandle.FromIntPtr(context).Target).Invoke(in value0, in value1, out value2); 
    } 
}

internal static class GarbagePrevention
{
    internal static readonly List<object> Objects = new();
}