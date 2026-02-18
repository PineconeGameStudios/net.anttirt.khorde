using System;
using System.Collections.Generic;
using AOT;
using Mpr.Blobs;
using Mpr.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Query
{
	public class QueryGraphAsset : BlobAsset<QSData>
	{
		public List<EntityQueryAsset> entityQueries;
		
#if UNITY_EDITOR
		private void OnEnable()
		{
			var icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/net.anttirt.khord/Icons/QueryGraph.psd");
			if(icon != null)
				UnityEditor.EditorGUIUtility.SetIconForObject(this, icon);
		}
#endif

		[MonoPInvokeCallback(typeof(BurstTrampolineOut<UnityObjectRef<QueryGraphAsset>, NativeArray<UnityObjectRef<EntityQueryAsset>>>.Delegate))]
		static void GetQueriesImpl(in UnityObjectRef<QueryGraphAsset> asset, out NativeArray<UnityObjectRef<EntityQueryAsset>> result)
		{
			result = default;
			
			try
			{
				var graphAsset = asset.Value;

				if(graphAsset == null || graphAsset.entityQueries == null)
					return;

				result = new NativeArray<UnityObjectRef<EntityQueryAsset>>(graphAsset.entityQueries.Count, Allocator.Temp);

				for(int i = 0; i < graphAsset.entityQueries.Count; i++)
					result[i] = graphAsset.entityQueries[i];
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		struct Ctx0 {}
		public static readonly SharedStatic<BurstTrampolineOut<UnityObjectRef<QueryGraphAsset>, NativeArray<UnityObjectRef<EntityQueryAsset>>>> GetQueriesFunc
			= SharedStatic<BurstTrampolineOut<UnityObjectRef<QueryGraphAsset>, NativeArray<UnityObjectRef<EntityQueryAsset>>>>
				.GetOrCreate<Ctx0>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		#if UNITY_EDITOR
		// NOTE: need InitializeOnLoadMethod for this to be available for unit tests
		[UnityEditor.InitializeOnLoadMethod]
		#endif
		static void StaticInit()
		{
			GetQueriesFunc.Data = new(GetQueriesImpl);
		}
		
		/// <summary>
		/// Get a list of references to entity query assets from the query graph asset
		/// </summary>
		/// <param name="asset"></param>
		/// <returns></returns>
		/// <remarks>Callable from Burst-compiled code</remarks>
		public static NativeArray<UnityObjectRef<EntityQueryAsset>> GetQueries(UnityObjectRef<QueryGraphAsset> asset)
		{
			GetQueriesFunc.Data.Invoke(asset, out var result);
			return result;
		}
	}
}
