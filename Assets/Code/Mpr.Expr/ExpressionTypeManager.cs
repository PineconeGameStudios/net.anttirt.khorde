using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Expr;

public struct ExpressionTypeInfo
{
    public readonly Type Type;
    public readonly ExpressionEvalDelegate Evaluate;
    public readonly bool IsBurstCompiled;

    public ExpressionTypeInfo(Type type, ExpressionEvalDelegate evaluate, bool isBurstCompiled)
    {
        Type = type;
        Evaluate = evaluate;
        IsBurstCompiled = isBurstCompiled;
    }
}

public static class ExpressionTypeManager
{
    private static bool s_initialized;
    
    private struct ExpressionTypeManagerKeyContext
    {
    }

    struct EvaluateFunctions
    {
        public static readonly SharedStatic<NativeHashMap<ulong, FunctionPointer<ExpressionEvalDelegate>>> Ref =
            SharedStatic<NativeHashMap<ulong, FunctionPointer<ExpressionEvalDelegate>>>
                .GetOrCreate<ExpressionTypeManagerKeyContext, EvaluateFunctions>();
    }

    public static bool TryGetEvaluateFunction(ulong stableTypeHash, out FunctionPointer<ExpressionEvalDelegate> function)
    {
        if (!EvaluateFunctions.Ref.Data.IsCreated)
            throw new InvalidOperationException("ExpressionTypeManager not initialized");
        
        return EvaluateFunctions.Ref.Data.TryGetValue(stableTypeHash, out function);
    }
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void Initialize()
    {
        if (s_initialized)
            return;
        
        s_initialized = true;
        
        var thisAssembly = typeof(ExpressionTypeManager).Assembly;
        var hashCache = new Dictionary<Type, ulong>();

        var functions = EvaluateFunctions.Ref.Data =
            new NativeHashMap<ulong, FunctionPointer<ExpressionEvalDelegate>>(0, Allocator.Domain);
        
        var directStorageSize = UnsafeUtility.SizeOf<ExpressionStorage>();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var refs = asm.GetReferencedAssemblies();
            if (asm != thisAssembly && Array.IndexOf(refs, thisAssembly) == -1)
                continue;

            var registryType = asm.GetType("Mpr.Expr.Generated.ExpressionTypeRegistry");

            if (registryType == null)
                continue;

            var types = registryType.GetField(nameof(Generated.ExpressionTypeRegistry.ExpressionTypes),
                        BindingFlags.Static | BindingFlags.Public)?
                    .GetValue(null)
                as ExpressionTypeInfo[];

            if (types == null)
                continue;

            foreach (var typeInfo in types)
            {
                var stableTypeHash = GetTypeHash(typeInfo.Type, hashCache);

                var evaluateDelegate = typeInfo.Evaluate;
                if (evaluateDelegate == null)
                    continue;

                FunctionPointer<ExpressionEvalDelegate> function;

                if (typeInfo.IsBurstCompiled)
                {
                    function = BurstCompiler.CompileFunctionPointer(evaluateDelegate);
                }
                else
                {
                    function = new FunctionPointer<ExpressionEvalDelegate>(
                        Marshal.GetFunctionPointerForDelegate(evaluateDelegate));
                }

                functions[stableTypeHash] = function;
            }
        }
    }

    public static ulong GetTypeHash<TExpression>(Dictionary<Type, ulong> hashCache) where TExpression : unmanaged
        => GetTypeHash(typeof(TExpression), hashCache);

    public static ulong GetTypeHash(Type expressionType, Dictionary<Type, ulong> hashCache)
    {
        var memoryOrdering =
            TypeHash.CalculateMemoryOrdering(expressionType, out bool hasCustomMemoryOrder, hashCache);
                
        return !hasCustomMemoryOrder
            ? memoryOrdering
            : TypeHash.CalculateStableTypeHash(expressionType, null, hashCache);
    }
}