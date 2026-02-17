using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Blobs.Authoring
{
	public static class BlobEntityQueryDescAuthoring
	{
	    private static
	        (
	            Dictionary<string, ulong> typeLookup,
	            Dictionary<string, List<Type>> ambiguousTypes
	        ) s_typeHashLookup
	            =
	        MakeTypeHashLookup();

	    private static (
	            Dictionary<string, ulong> typeLookup,
	            Dictionary<string, List<Type>> ambiguousTypes
	        ) MakeTypeHashLookup()
	    {
	        var types = TypeManager.GetAllTypes();
	        var typeDictionary = new Dictionary<string, ulong>();
	        var ambiguousTypes = new Dictionary<string, List<Type>>();
	        foreach (var type in types)
	        {
	            var managedType = type.Type;
            
	            if(managedType == null)
	                continue;
            
	            if (!ambiguousTypes.ContainsKey(managedType.Name))
	            {
	                if (!typeDictionary.TryAdd(managedType.Name, type.StableTypeHash))
	                {
	                    if(!ambiguousTypes.TryGetValue(managedType.Name, out var ambs))
	                        ambs = ambiguousTypes[managedType.Name] = new  List<Type>();
	                    ambs.Add(managedType);
	                    typeDictionary.Remove(managedType.Name);
	                }
	            }

	            if (!string.IsNullOrWhiteSpace(managedType.Namespace))
	            {
	                typeDictionary[managedType.Namespace + "." + managedType.Name] = type.StableTypeHash;
	            }
	        }

	        return (typeDictionary, ambiguousTypes);
	    }
    
	    /// <summary>
	    /// Parse and bake a string query description into blob format
	    /// </summary>
	    /// <param name="entityQueryDesc"></param>
	    /// <param name="description"></param>
	    /// <param name="blobBuilder"></param>
	    public static void Bake(ref this BlobEntityQueryDesc entityQueryDesc, string description, ref BlobBuilder blobBuilder, Action<string> logError)
	    {
	        var all = new NativeList<ulong>(Allocator.Temp);
	        var any = new NativeList<ulong>(Allocator.Temp);
	        var none = new NativeList<ulong>(Allocator.Temp);
	        var disabled = new NativeList<ulong>(Allocator.Temp);
	        var absent = new NativeList<ulong>(Allocator.Temp);
	        var present = new NativeList<ulong>(Allocator.Temp);
	        entityQueryDesc.pendingOptions = default;

	        var (typeLookup, ambLookup) = s_typeHashLookup;

	        void AddTypes(NativeList<ulong> dst, string src)
	        {
	            foreach (var ctype in src.Split(',', StringSplitOptions.RemoveEmptyEntries))
	            {
	                var trimmed = ctype.Trim();
	                if (typeLookup.TryGetValue(trimmed, out ulong stableTypeHash))
	                    dst.Add(stableTypeHash);
	                else if (ambLookup.TryGetValue(trimmed, out var ambs))
	                    logError($"type name '{trimmed}' is ambiguous between [{(string.Join(", ", ambs.Select(t => t.FullName)))}]");
	                else
	                    logError($"type name '{trimmed}' is unknown");
	            }
	        }
        
	        foreach (var line in description.Split(new char[]{'\n', ';'}, StringSplitOptions.RemoveEmptyEntries))
	        {
	            var parts = line.Split(':');

	            if (parts.Length != 2)
	            {
	                logError($"could not parse '{line}'");
	                continue;
	            }
            
	            var value = parts[1].Trim();
	            switch (parts[0].Trim())
	            {
	                case "all": AddTypes(all, value); break;
	                case "any": AddTypes(any, value); break;
	                case "none": AddTypes(none, value); break;
	                case "disabled": AddTypes(disabled, value); break;
	                case "absent": AddTypes(absent, value); break;
	                case "present": AddTypes(present, value); break;
	                case "options":
	                    if (Enum.TryParse<EntityQueryOptions>(value, true, out var options))
	                        entityQueryDesc.pendingOptions |= options;
	                    else
	                        logError($"could not parse '{value}' as EntityQueryOptions");
	                    break;
	            }
	        }

	        void CopyComponentTypes(ref BlobArray<BlobComponentType> comps, NativeList<ulong> src, ref BlobBuilder blobBuilder)
	        {
	            var dst = blobBuilder.Allocate(ref comps, src.Length);
	            for (int i = 0; i < src.Length; ++i)
	                dst[i] = new BlobComponentType() { stableTypeHash = src[i], accessModeType = ComponentType.AccessMode.ReadOnly };
	        }

	        CopyComponentTypes(ref entityQueryDesc.all, all, ref blobBuilder);
	        CopyComponentTypes(ref entityQueryDesc.any, any, ref blobBuilder);
	        CopyComponentTypes(ref entityQueryDesc.none, none, ref blobBuilder);
	        CopyComponentTypes(ref entityQueryDesc.disabled, disabled, ref blobBuilder);
	        CopyComponentTypes(ref entityQueryDesc.absent, absent, ref blobBuilder);
	        CopyComponentTypes(ref entityQueryDesc.present, present, ref blobBuilder);
	    }

	    /// <summary>
	    /// Read the entity query description back into a text format
	    /// </summary>
	    /// <param name="entityQueryDesc"></param>
	    /// <returns></returns>
	    public static string Unbake(ref this BlobEntityQueryDesc entityQueryDesc)
	    {
	        var sb = new StringBuilder();
        
	        WriteComponents(sb, ref entityQueryDesc.all, "all");
	        WriteComponents(sb, ref entityQueryDesc.any, "any");
	        WriteComponents(sb, ref entityQueryDesc.none, "none");
	        WriteComponents(sb, ref entityQueryDesc.disabled, "disabled");
	        WriteComponents(sb, ref entityQueryDesc.absent, "absent");
	        WriteComponents(sb, ref entityQueryDesc.present, "present");
        
	        if (entityQueryDesc.pendingOptions != default)
	        {
	            sb.Append("options: ");
	            sb.Append(entityQueryDesc.pendingOptions.ToString());
	        }
        
	        return sb.ToString();

	        static void WriteComponents(StringBuilder stringBuilder, ref BlobArray<BlobComponentType> blobArray, string name)
	        {
	            if (blobArray.Length > 0)
	            {
	                stringBuilder.Append(name);
	                stringBuilder.Append(": ");
	                var sep = "";
	                foreach (ref var ctype in blobArray.AsSpan())
	                {
	                    stringBuilder.Append(sep);
	                    stringBuilder.Append(ctype.ResolveComponentType().GetManagedType().FullName);
	                    sep = ", ";
	                }

	                stringBuilder.Append("\n");
	            }
	        }
	    }
	}
}