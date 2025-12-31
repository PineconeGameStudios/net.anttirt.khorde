using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using Debug = UnityEngine.Debug;

namespace Mpr.Expr;

/// <summary>
/// Store strong asset references so the baking system can find them.
/// </summary>
public struct BlobExpressionObjectReference : IBufferElementData
{
    public UnityObjectRef<UnityEngine.Object> asset;
}
    
/// <summary>
/// Store weak asset references so the baking system can find them.
/// </summary>
public struct BlobExpressionWeakObjectReference : IBufferElementData
{
    public WeakObjectReference<UnityEngine.Object> asset;
}

/// <summary>
/// Component field layout info for reading and writing field values
/// </summary>
public struct ExpressionComponentTypeInfo
{
    public struct Field
    {
        public ushort offset;
        public ushort length;

        public static implicit operator Field(System.Reflection.FieldInfo fieldInfo)
        {
            return new Field
            {
                offset = (ushort)UnsafeUtility.GetFieldOffset(fieldInfo),
                length = (ushort)UnsafeUtility.SizeOf(fieldInfo.FieldType),
            };
        }

        public override string ToString()
        {
            return $"{{ offset={offset}, length={length} }}";
        }
    }
        
    public int componentIndex;
    public BlobArray<Field> fields;
}

/// <summary>
/// This needs to be binary-compatible with <see cref="UnityObjectRef{T}"/>
/// </summary>
public struct UntypedExpressionObjectId
{
    // TODO: add a preprocessor branch when UnityObjectRef switches to 64-bit EntityId
    public int instanceId;

    public static UntypedExpressionObjectId FromUnityObjectRef<T>(UnityObjectRef<T> r) where T : UnityEngine.Object
    {
        UntypedExpressionObjectId result = default;
        unsafe
        {
            result = *(UntypedExpressionObjectId*)&r;
        }

        return result;
    }
}
    
/// <summary>
/// Version of <see cref="UnityObjectRef{T}"/> that can be stored
/// in an <see cref="ExpressionData"/> blob.
/// Used with expression blob data. The baker stores a separate
/// UnityObjectRef in a patch buffer that also guarantees
/// the object is available when the subscene is loaded.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct ExpressionObjectRef<T> where T : UnityEngine.Object
{
    public UntypedExpressionObjectId objectId;

    public T Value
    {
        get
        {
            unsafe
            {
                fixed (void* ptr = &objectId)
                    return ((UnityObjectRef<T>*)ptr)->Value;
            }
        }
    }
}
    
/// <summary>
/// Version of <see cref="WeakObjectReference{TObject}"/> that
/// can be stored in an <see cref="ExpressionData"/> blob.
/// </summary>
/// <typeparam name="TObject"></typeparam>
public struct WeakExpressionObjectRef<TObject> where TObject : UnityEngine.Object
{
    public RuntimeGlobalObjectId GlobalId;
    public WeakReferenceGenerationType GenerationType;

    public WeakObjectReference<TObject> AsWeakObjectReference => new(new UntypedWeakReferenceId(GlobalId, GenerationType));
}

public unsafe ref struct ExpressionEvalContext
{
    public ExpressionEvalContext(
        ref BlobExpressionData data,
        NativeArray<UnsafeComponentReference> componentPtrs,
        NativeArray<UntypedComponentLookup> componentLookups)
    {
        fixed (BlobExpressionData* pData = &data)
            this.dataPtr = pData;
        this.componentPtrs = componentPtrs;
        this.componentLookups = componentLookups;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        this.callStack = new(Allocator.Temp);
        this.callStackLookup = new(8, Allocator.Temp);
#endif
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    public ExpressionEvalContext(
        ref BlobExpressionData data,
        NativeArray<UnsafeComponentReference> componentPtrs,
        NativeArray<UntypedComponentLookup> componentLookups,
        NativeList<ushort> callStack,
        NativeHashSet<ushort> callStackLookup)
    {
        fixed (BlobExpressionData* pData = &data)
            this.dataPtr = pData;
        this.componentPtrs = componentPtrs;
        this.componentLookups = componentLookups;
        this.callStack = callStack;
        this.callStackLookup = callStackLookup;
    }
#endif

    BlobExpressionData* dataPtr;
    public NativeArray<UnsafeComponentReference> componentPtrs;
    public NativeArray<UntypedComponentLookup> componentLookups;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    public NativeList<ushort> callStack;
    public NativeHashSet<ushort> callStackLookup;
#endif

    public ref BlobExpressionData data => ref *dataPtr;
}

public static class ExprNativeArrayExt
{
    /// <summary>
    /// Interpret the slice as a single element of type <typeparamref name="T"/>
    /// </summary>
    /// <param name="slice"></param>
    /// <typeparam name="T"><c>unmanaged</c> type to interpret slice contents as</typeparam>
    /// <returns></returns>
    public static ref T AsSingle<T>(this ref NativeArray<byte> slice) where T : unmanaged
    {
        unsafe
        {
            var converted = slice.Reinterpret<T>(1);
            return ref *(T*)converted.GetUnsafePtr();
        }
    }

    /// <summary>
    /// Clear the contents of the slice, setting all bytes to zero.
    /// </summary>
    /// <param name="slice"></param>
    public static void Clear(this ref NativeArray<byte> slice)
    {
        unsafe
        {
            UnsafeUtility.MemClear(slice.GetUnsafePtr(), slice.Length);
        }
    }
}

/// <summary>
/// Used to define the inputs for an expression. Can reference other expressions and bake-time constant values.
/// </summary>
public struct ExpressionRef
{
    private readonly ushort index;
    private readonly ushort packedIndexOrLength;
    private const ushort FlagConstant = (ushort)0x8000u;
    private const ushort IndexMask = (ushort)0x7fffu;
        
    private bool isConstant
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (packedIndexOrLength & FlagConstant) != 0;
    }

    private ushort outputIndexOrConstantLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { unchecked { return (ushort)(packedIndexOrLength & IndexMask); } }
    }

    ExpressionRef(ushort index, ushort outputIndex, bool constant)
    {
        this.index = index;
        this.packedIndexOrLength = (ushort)(outputIndex | (constant ? FlagConstant : 0u));
    }

    public static ExpressionRef Node(ushort index, ushort outputIndex) => new ExpressionRef(index, outputIndex, false);
    public static ExpressionRef Const(ushort offset, ushort length) => new ExpressionRef(offset, length, true);

    public ExpressionRef WithOutputIndex(ushort outputIndex) => Node(index, outputIndex);

    public T Evaluate<T>(in ExpressionEvalContext ctx) where T : unmanaged
    {
        if (isConstant)
        {
            return ctx.data.GetConstant<T>(index);
        }
        else
        {
            T result = default;

            unsafe
            {
                var size = UnsafeUtility.SizeOf<T>();
                var resultSlice =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(&result, size, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref resultSlice,
                    AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                CheckStackPush(in ctx, index);
                ctx.data.expressions[index].Evaluate(in ctx, outputIndexOrConstantLength, ref resultSlice);
                CheckStackPop(in ctx, index);
            }

            return result;
        }
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private void CheckStackPush(in ExpressionEvalContext ctx, ushort index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (!ctx.callStack.IsCreated)
            return;
        
        if (!ctx.callStackLookup.Add(index))
            throw new InvalidOperationException($"expression cycle: {index} calls itself");
                
        ctx.callStack.Add(index);
#endif
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    private void CheckStackPop(in ExpressionEvalContext ctx, ushort index)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if (!ctx.callStack.IsCreated)
            return;
        
        var top = ctx.callStack[ctx.callStack.Length - 1];
        if (top != index)
            throw new InvalidOperationException($"inconsistent stack: {index} call resulted in {top} at the top");
        
        ctx.callStackLookup.Remove(top);
        ctx.callStack.RemoveAt(ctx.callStack.Length - 1);
#endif
    }

    public void Evaluate<T>(in ExpressionEvalContext ctx, out T result) where T : unmanaged
    {
        if (isConstant)
        {
            result = ctx.data.GetConstant<T>(index);
        }
        else
        {
            unsafe
            {
                var size = UnsafeUtility.SizeOf<T>();
                fixed (T* pResult = &result)
                {
                    var resultSlice =
                        NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<byte>(pResult, size, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref resultSlice,
                        AtomicSafetyHandle.GetTempMemoryHandle());
#endif

                    CheckStackPush(in ctx, index);
                    ctx.data.expressions[index].Evaluate(in ctx, outputIndexOrConstantLength, ref resultSlice);
                    CheckStackPop(in ctx, index);
                }
            }
        }
    }

    public void Evaluate(in ExpressionEvalContext ctx, ref NativeArray<byte> result)
    {
        if (isConstant)
        {
            result.CopyFrom(
                ctx.data.GetConstants()
                    .GetSubArray(index, outputIndexOrConstantLength)
            );
        }
        else
        {
            CheckStackPush(in ctx, index);
            ctx.data.expressions[index].Evaluate(in ctx, outputIndexOrConstantLength, ref result);
            CheckStackPop(in ctx, index);
        }
    }

    public override string ToString()
    {
        return isConstant ? $"const(off={index}, sz={outputIndexOrConstantLength}) " : $"ref(expr={index} out={outputIndexOrConstantLength})";
    }
}

public interface IExpressionBase { }

public interface IExpression : IExpressionBase
{
    void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult);
}

public interface IExpression<T0> : IExpressionBase where T0 : unmanaged
{
    ExpressionRef Input0 { get; }
    void Evaluate(in ExpressionEvalContext ctx, in T0 input0, int outputIndex, ref NativeArray<byte> untypedResult);
}

public interface IExpression<T0, T1> : IExpressionBase where T0 : unmanaged where T1 : unmanaged
{
    ExpressionRef Input0 { get; }
    ExpressionRef Input1 { get; }
    void Evaluate(in ExpressionEvalContext ctx, in T0 input0, in T1 input1, int outputIndex, ref NativeArray<byte> untypedResult);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void ExpressionEvalDelegate(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
    ref NativeArray<byte> untypedResult);

static unsafe class EvalHelper
{
    [BurstDiscard]
    static void TestBurst(ref bool isBurst) => isBurst = false;

    public static void ReportBurst()
    {
        bool isBurst = true;
        TestBurst(ref isBurst);
        
        if (isBurst)
            Debug.Log("running from burst");
        else
            Debug.Log("running from mono/il2cpp");
    }

    public static void Evaluate<TExpr>(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
        where TExpr : unmanaged, IExpression
    {
        TExpr* expr = self->GetUnsafePtr<TExpr>();
        expr->Evaluate(in ctx, outputIndex, ref untypedResult);
    }

    public static void Evaluate<TExpr, TInput0>(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
        where TExpr : unmanaged, IExpression<TInput0>
        where TInput0 : unmanaged
    {
        TExpr* expr = self->GetUnsafePtr<TExpr>();
        expr->Input0.Evaluate(in ctx, out TInput0 input0);
        expr->Evaluate(in ctx, in input0, outputIndex, ref untypedResult);
    }

    public static void Evaluate<TExpr, TInput0, TInput1>(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
        where TExpr : unmanaged, IExpression<TInput0, TInput1>
        where TInput0 : unmanaged
        where TInput1 : unmanaged
    {
        TExpr* expr = self->GetUnsafePtr<TExpr>();
        expr->Input0.Evaluate(in ctx, out TInput0 input0);
        expr->Input1.Evaluate(in ctx, out TInput1 input1);
        expr->Evaluate(in ctx, in input0, in input1, outputIndex, ref untypedResult);
    }
}

/// <summary>
/// Blob storage for expression data.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct ExpressionStorage
{
    // 16 bytes of inline storage, fits for example 4 input node references.
    // This covers the most common functionality: unary and binary operators, and component lookups.
    [FieldOffset(0)] private long s0;
    [FieldOffset(8)] private long s1;
        
    // if that's not enough, use an indirect pointer (into the same blob)
    [FieldOffset(0)] public BlobPtr<byte> dataReference;

    public unsafe TExpr* GetUnsafePtr<TExpr>() where TExpr : unmanaged
    {
        if (sizeof(TExpr) <= sizeof(ExpressionStorage))
        {
            fixed (long* ptr = &s0)
                return (TExpr*)ptr;
        }
        else
        {
            return (TExpr*)dataReference.GetUnsafePtr();
        }
    }

    public ref BlobPtr<T> GetDataReference<T>() where T : unmanaged
    {
        unsafe
        {
            fixed(BlobPtr<byte>* pref = &dataReference)
                return ref *(BlobPtr<T>*)pref;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct ExpressionData
{
    public ExpressionStorage storage;

    // filled in at runtime
    public long evaluateFuncPtr;

    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeArray<byte> untypedResult)
    {
        unsafe
        {
            var funcPtr = new FunctionPointer<ExpressionEvalDelegate>((IntPtr)evaluateFuncPtr);
            fixed (ExpressionStorage* ptr = &storage)
            {
                funcPtr.Invoke(ptr, in ctx, outputIndex, ref untypedResult);
            }
        }
    }
}