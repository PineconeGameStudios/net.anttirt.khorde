using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Khorde.Blobs;
using Khorde.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Khorde.Query
{
	public class QueryGraphAsset : BlobAsset<QSData>
	{
		public List<EntityQueryAsset> entityQueries;
		
#if UNITY_EDITOR
		private void OnEnable()
		{
			var icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/net.anttirt.khorde/Icons/QueryGraph.psd");
			if(icon != null)
				UnityEditor.EditorGUIUtility.SetIconForObject(this, icon);
		}
#endif

		public unsafe delegate void GetQueriesDelegate(UnityObjectRef<QueryGraphAsset>* passet, NativeArray<UnityObjectRef<EntityQueryAsset>>* presult);
		static GetQueriesDelegate s_getQueriesGC;

		[MonoPInvokeCallback(typeof(GetQueriesDelegate))]
		static unsafe void GetQueriesImpl(UnityObjectRef<QueryGraphAsset>* asset, NativeArray<UnityObjectRef<EntityQueryAsset>>* result)
		{
			*result = default;
			
			try
			{
				var graphAsset = asset->Value;

				if(graphAsset == null || graphAsset.entityQueries == null)
					return;

				*result = new NativeArray<UnityObjectRef<EntityQueryAsset>>(graphAsset.entityQueries.Count, Allocator.Temp);

				for(int i = 0; i < graphAsset.entityQueries.Count; i++)
					(*result)[i] = graphAsset.entityQueries[i];
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		struct Ctx1 {}
		public static readonly SharedStatic<FunctionPointer<GetQueriesDelegate>> GetQueriesFunc
			= SharedStatic<FunctionPointer<GetQueriesDelegate>>
				.GetOrCreate<Ctx1>();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		#if UNITY_EDITOR
		// NOTE: need InitializeOnLoadMethod for this to be available for unit tests
		[UnityEditor.InitializeOnLoadMethod]
		#endif
		static void StaticInit()
		{
			unsafe
			{
				s_getQueriesGC = GetQueriesImpl;
				GetQueriesFunc.Data = new FunctionPointer<GetQueriesDelegate>(Marshal.GetFunctionPointerForDelegate(s_getQueriesGC));
			}
		}
		
		/// <summary>
		/// Get a list of references to entity query assets from the query graph asset
		/// </summary>
		/// <param name="asset"></param>
		/// <returns></returns>
		/// <remarks>Callable from Burst-compiled code</remarks>
		public static NativeArray<UnityObjectRef<EntityQueryAsset>> GetQueries(UnityObjectRef<QueryGraphAsset> asset)
		{
			unsafe
			{
				NativeArray<UnityObjectRef<EntityQueryAsset>> result = default;
				GetQueriesFunc.Data.Invoke(&asset, &result);
				return result;
			}
		}
	}
}
