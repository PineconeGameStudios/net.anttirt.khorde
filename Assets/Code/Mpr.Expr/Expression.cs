using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine;

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

public struct UntypedExpressionObjectRef
{
    /// <summary>
    /// Runtime-initialized instance id
    /// </summary>
    public int instanceId;
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
/// Version of <see cref="UnityObjectRef{T}"/> that can be stored
/// in an <see cref="ExpressionData"/> blob.
/// Used with expression blob data. The baker stores a separate
/// UnityObjectRef in a patch buffer that also guarantees
/// the object is available when the subscene is loaded.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct ExpressionObjectRef<T> where T : UnityEngine.Object
{
    public UntypedExpressionObjectRef objectRef;

    public T Value
    {
        get
        {
            unsafe
            {
                fixed (void* ptr = &objectRef.instanceId)
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

public struct BlobExpressionData
{
    /// <summary>
    /// Storage for constant-valued expression node references
    /// </summary>
    public BlobArray<byte> constants;

    /// <summary>
    /// Storage for expressions
    /// </summary>
    public BlobArray<ExpressionData> expressions;

    /// <summary>
    /// Used after loading an expression data blob to populate the function pointers in the <see cref="expressions"/> array
    /// </summary>
    public BlobArray<ulong> expressionTypeHashes;

    /// <summary>
    /// Internal pointers to patch strong asset references at runtime
    /// </summary>
    public BlobArray<BlobPtr<UntypedExpressionObjectRef>> patchableObjectRefs;

    /// <summary>
    /// Component types (especially non-[ChunkSerializable] ones) might have a
    /// different layout on the target platform so we have to initialize layouts
    /// at runtime
    /// </summary>
    public BlobArray<BlobPtr<ExpressionComponentTypeInfo>> patchableTypeInfos;

    /// <summary>
    /// Used after loading an expression data blob to populate the field offsets
    /// in ExpressionComponentTypeInfo
    /// </summary>
    public BlobArray<ulong> typeInfoTypeHashes;

    /// <summary>
    /// Get constants buffer as a NativeSlice
    /// </summary>
    /// <returns></returns>
    public NativeSlice<byte> GetConstants()
    {
        unsafe
        {
            return NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<byte>(
                constants.GetUnsafePtr(),
                1,
                constants.Length);
        }
    }

    private bool isRuntimeInitialized;
    private ObjectLoadingStatus loadingStatus;

    /// <summary>
    /// Whether <see cref="RuntimeInitialize"/> has been called on this instance.
    /// </summary>
    public bool IsRuntimeInitialized => isRuntimeInitialized;
        
    /// <summary>
    /// Latest loading status for weakly referenced assets.
    /// </summary>
    public ObjectLoadingStatus LoadingStatus => loadingStatus;

    /// <summary>
    /// Initialize expression function pointers, patch strong object refs, start loading weak object refs, etc.
    /// </summary>
    public void RuntimeInitialize(
        DynamicBuffer<BlobExpressionObjectReference> objectReferences,
        DynamicBuffer<BlobExpressionWeakObjectReference> weakObjectReferences
        )
    {
        if (isRuntimeInitialized)
            return;

        isRuntimeInitialized = true;

        for (int i = 0; i < patchableObjectRefs.Length; ++i)
        {
            ref var patchableObjectRef = ref patchableObjectRefs[i];
            var assetRef = objectReferences[i].asset;
            unsafe
            {
                patchableObjectRef.Value.instanceId = *(int*)&assetRef;
            }
        }

        foreach (var weakObjectReference in weakObjectReferences)
        {
            weakObjectReference.asset.LoadAsync();
        }
            
        UpdateLoadingStatus(objectReferences, weakObjectReferences);
            
        if (expressions.Length != expressionTypeHashes.Length)
        {
            Debug.LogError("corrupted data: must have the same amount of expressions and expression type hashes");
            return;
        }

        for (int i = 0; i < expressions.Length; ++i)
        {
            var typeHash = expressionTypeHashes[i];
            if (ExpressionTypeManager.TryGetEvaluateFunction(typeHash, out var function))
            {
                expressions[i].evaluateFuncPtr = (long)function.Value;
            }
            else
            {
                Debug.LogError($"couldn't find generated type info for hash {typeHash}");
            }
        }
    }

    public void UpdateLoadingStatus(DynamicBuffer<BlobExpressionObjectReference> objectReferences,
        DynamicBuffer<BlobExpressionWeakObjectReference> weakObjectReferences)
    {
        if (loadingStatus == ObjectLoadingStatus.Completed)
            return;

        foreach (var weakObjectReference in weakObjectReferences)
        {
            if (weakObjectReference.asset.LoadingStatus != ObjectLoadingStatus.Completed)
            {
                loadingStatus = weakObjectReference.asset.LoadingStatus;
                return;
            }
        }

        loadingStatus = ObjectLoadingStatus.Completed;
    }

    /// <summary>
    /// Release weak asset references, etc.
    /// </summary>
    /// <param name="objectReferences"></param>
    /// <param name="weakObjectReferences"></param>
    public void RuntimeDeinitialize(DynamicBuffer<BlobExpressionObjectReference> objectReferences, DynamicBuffer<BlobExpressionWeakObjectReference> weakObjectReferences)
    {
        if (!isRuntimeInitialized)
            return;

        isRuntimeInitialized = false;
            
        foreach (var weakObjectReference in weakObjectReferences)
        {
            weakObjectReference.asset.Release();
        }
    }
}

public unsafe ref struct ExpressionEvalContext
{
    public ExpressionEvalContext(
        ref BlobExpressionData data,
        NativeSlice<UnsafeComponentReference> componentPtrs,
        NativeSlice<UntypedComponentLookup> componentLookups
    )
    {
        fixed (BlobExpressionData* pData = &data)
            this.dataPtr = pData;
        this.componentPtrs = componentPtrs;
        this.componentLookups = componentLookups;
    }

    BlobExpressionData* dataPtr;
    public NativeSlice<UnsafeComponentReference> componentPtrs;
    public NativeSlice<UntypedComponentLookup> componentLookups;

    public ref BlobExpressionData data => ref *dataPtr;
}

/// <summary>
/// Used to reference other expressions or constants from an expression.
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

    public T Evaluate<T>(in ExpressionEvalContext ctx) where T : unmanaged
    {
        if (isConstant)
        {
            return ctx.data.GetConstants()
                .Slice(index, outputIndexOrConstantLength)
                .SliceConvert<T>()[0];
        }

        T result = default;

        unsafe
        {
            var size = UnsafeUtility.SizeOf<T>();
            var resultSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<byte>(&result, size, size);
            ctx.data.expressions[index].Evaluate(in ctx, outputIndexOrConstantLength, ref resultSlice);
        }

        return result;
    }

    public void Evaluate(in ExpressionEvalContext ctx, ref NativeSlice<byte> result)
    {
        if (isConstant)
        {
            result.CopyFrom(
                ctx.data.GetConstants()
                    .Slice(index, outputIndexOrConstantLength)
            );
        }
        else
        {
            ctx.data.expressions[index].Evaluate(in ctx, outputIndexOrConstantLength, ref result);
        }
    }

    public override string ToString()
    {
        return isConstant ? $"const(off={index}, sz={outputIndexOrConstantLength}) " : $"ref(expr={index} out={outputIndexOrConstantLength})";
    }
}

public interface IExpression
{
    void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void EvaluateDelegate2(ExpressionStorage* self, in ExpressionEvalContext ctx, int outputIndex,
    ref NativeSlice<byte> untypedResult);

[StructLayout(LayoutKind.Explicit)]
public struct ExpressionStorage
{
    // 16 bytes of inline storage, fits for example 4 input node references.
    // This covers the most common functionality: unary and binary operators, and component lookups.
    [FieldOffset(0)] private long s0;
    [FieldOffset(8)] private long s1;
        
    // if that's not enough, use an indirect pointer (into the same blob)
    [FieldOffset(0)] public BlobPtr<byte> dataReference;
}

[StructLayout(LayoutKind.Sequential)]
public struct ExpressionData
{
    ExpressionStorage storage;

    // filled in at runtime
    public long evaluateFuncPtr;

    public void Evaluate(in ExpressionEvalContext ctx, int outputIndex, ref NativeSlice<byte> untypedResult)
    {
        unsafe
        {
            fixed (ExpressionStorage* ptr = &storage)
                new FunctionPointer<EvaluateDelegate2>((IntPtr)evaluateFuncPtr).Invoke(ptr, in ctx, outputIndex,
                    ref untypedResult);
        }
    }
}