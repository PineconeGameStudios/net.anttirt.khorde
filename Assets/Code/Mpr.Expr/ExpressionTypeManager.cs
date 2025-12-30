using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Expr;

public struct ExpressionTypeInfo
{
    public readonly Type Type;
    public readonly bool IsBurstCompiled;

    public ExpressionTypeInfo(Type type, bool isBurstCompiled)
    {
        Type = type;
        IsBurstCompiled = isBurstCompiled;
    }
}

public static class ExpressionTypeManager
{
    private struct ExpressionTypeManagerKeyContext
    {
    }

    struct EvaluateFunctions
    {
        public static readonly SharedStatic<NativeHashMap<ulong, FunctionPointer<EvaluateDelegate2>>> Ref =
            SharedStatic<NativeHashMap<ulong, FunctionPointer<EvaluateDelegate2>>>
                .GetOrCreate<ExpressionTypeManagerKeyContext, EvaluateFunctions>();
    }

    // ReSharper disable once CollectionNeverQueried.Local
    private static List<EvaluateDelegate2> s_gcRoots;

    public static bool TryGetEvaluateFunction(ulong stableTypeHash, out FunctionPointer<EvaluateDelegate2> function)
    {
        if (EvaluateFunctions.Ref.Data.IsCreated &&
            EvaluateFunctions.Ref.Data.TryGetValue(stableTypeHash, out function))
            return true;

        function = default;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    public static void Initialize()
    {
        var thisAssembly = typeof(ExpressionTypeManager).Assembly;
        var hashCache = new Dictionary<Type, ulong>();
        s_gcRoots = new List<EvaluateDelegate2>();

        var functions = EvaluateFunctions.Ref.Data =
            new NativeHashMap<ulong, FunctionPointer<EvaluateDelegate2>>(0, Allocator.Domain);

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
                var memoryOrdering =
                    TypeHash.CalculateMemoryOrdering(typeInfo.Type, out bool hasCustomMemoryOrder, hashCache);
                var stableTypeHash = !hasCustomMemoryOrder
                    ? memoryOrdering
                    : TypeHash.CalculateStableTypeHash(typeInfo.Type, null, hashCache);

                var method = typeInfo.Type.GetMethod("Evaluate", BindingFlags.Static | BindingFlags.Public);
                if (method == null)
                    continue;

                var evaluateDelegate = method.CreateDelegate(typeof(EvaluateDelegate2)) as EvaluateDelegate2;
                if (evaluateDelegate == null)
                    continue;

                FunctionPointer<EvaluateDelegate2> function;

                if (typeInfo.IsBurstCompiled)
                {
                    function = BurstCompiler.CompileFunctionPointer(evaluateDelegate);
                }
                else
                {
                    s_gcRoots.Add(evaluateDelegate);
                    function = new FunctionPointer<EvaluateDelegate2>(
                        Marshal.GetFunctionPointerForDelegate(evaluateDelegate));
                }

                functions[stableTypeHash] = function;
            }
        }
    }
}