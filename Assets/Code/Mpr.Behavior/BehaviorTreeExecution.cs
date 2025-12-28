using Mpr.Blobs;
using Mpr.Expr;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Mpr.Behavior
{
	public static class BehaviorTreeExecution
	{

		public static void Execute(this BlobAssetReference<BTData> asset, ref BTState state, DynamicBuffer<BTStackFrame> stack, ReadOnlySpan<UnsafeComponentReference> componentPtrs, ReadOnlySpan<UntypedComponentLookup> lookups, float now, DynamicBuffer<BTExecTrace> trace)
			=> Execute(ref asset.Value, ref state, stack, componentPtrs, lookups, now, trace);

		public static void Execute(ref BTData data, ref BTState state, DynamicBuffer<BTStackFrame> stack, ReadOnlySpan<UnsafeComponentReference> componentPtrs, ReadOnlySpan<UntypedComponentLookup> lookups, float now, DynamicBuffer<BTExecTrace> trace)
		{
			if (data.exprData.localComponents.Length > componentPtrs.Length)
			{
				var missing = new NativeHashSet<TypeIndex>(0, Allocator.Temp);
				foreach (ref var bct in data.exprData.localComponents.AsSpan())
					missing.Add(bct.ResolveComponentType().TypeIndex);
				
				foreach(var cptr in componentPtrs)
					missing.Remove(cptr.typeIndex);
				
				foreach(var m in missing)
					Debug.LogError($"missing local component {TypeManager.GetTypeInfo(m).DebugTypeName}");
				
				throw new Exception($"not enough components; bt requires {data.exprData.localComponents.Length} but only {componentPtrs.Length} found");
			}

			if(data.exprData.localComponents.Length < componentPtrs.Length)
				throw new Exception($"too many components; bt requires {data.exprData.localComponents.Length} but {componentPtrs.Length} found");

			for(int i = 0; i < data.exprData.localComponents.Length; ++i)
				if(data.exprData.localComponents[i].stableTypeHash != componentPtrs[i].stableTypeHash)
					throw new Exception($"wrong type at index {i}, expected " +
						$"{TypeManager.GetTypeInfo(TypeManager.GetTypeIndexFromStableTypeHash(data.exprData.localComponents[i].stableTypeHash)).DebugTypeName}, found" +
						$"{TypeManager.GetTypeInfo(componentPtrs[i].typeIndex).DebugTypeName}");

			for (int i = 0; i < data.exprData.lookupComponents.Length; ++i)
			{
				if (!lookups[i].IsCreated)
					throw new Exception($"component lookup at index {i} was not created");
				
				// TODO: this is an expensive check, remove it somehow
				if (data.exprData.lookupComponents[i].ResolveComponentType().TypeIndex != lookups[i].TypeIndex)
					throw new Exception($"wrong type at index {i}, expected " +
					                    $"{TypeManager.GetTypeInfo(TypeManager.GetTypeIndexFromStableTypeHash(data.exprData.lookupComponents[i].stableTypeHash)).DebugTypeName}, found" +
					                    $"{TypeManager.GetTypeInfo(lookups[i].TypeIndex).DebugTypeName}");
			}

			if(stack.Length == 0)
			{
				if(trace.IsCreated)
					trace.Add(new(data.Root, BTExec.BTExecType.Root, BTExecTrace.Event.Init, stack.Length, -1));

				stack.Add(data.Root);
			}

			var exprContext = new ExprEvalContext(ref data.exprData, componentPtrs, lookups);
			
			bool rootVisited = false;

			for(int cycle = 0; ; ++cycle)
			{
				if(cycle > 10000)
					throw new Exception("max cycle count exceeded; almost certainly a bug in the implementation");

				var nodeId = stack[^1].nodeId;

				ref BTExec node = ref data.GetNode(nodeId);

				if(trace.IsCreated && cycle == 0)
					trace.Add(new(nodeId, node.type, BTExecTrace.Event.Start, stack.Length, cycle));

				if(cycle == 0 && node.type != BTExec.BTExecType.Root && node.type != BTExec.BTExecType.Wait)
					throw new InvalidOperationException($"BUG: Execute() started with node type {node.type}");

				void Trace(ref BTExec node, BTExecTrace.Event @event)
				{
					if(trace.IsCreated)
						trace.Add(new(nodeId, node.type, @event, stack.Length, cycle));
				}

				void Trace1(ref BTData data, BTExecTrace.Event @event)
				{
					if(trace.IsCreated)
						trace.Add(new(nodeId, data.GetNode(nodeId).type, @event, stack.Length, cycle));
				}

				void Trace2(ref BTData data, int stackIndex, BTExecTrace.Event @event)
				{
					if(trace.IsCreated)
						trace.Add(new(stack[stackIndex].nodeId, data.GetNode(stack[stackIndex].nodeId).type, @event, stackIndex + 1, cycle));
				}

				void Fail(ref BTData data, ref BTExec node)
				{
					Trace(ref node, BTExecTrace.Event.Fail);

					for(int i = stack.Length - 1; i > 0; --i)
					{
						ref var stackNode = ref data.GetNode(stack[i].nodeId);
						if(stackNode.type == BTExec.BTExecType.Catch)
						{
							Trace2(ref data, i, BTExecTrace.Event.Catch);
							stack.RemoveRange(i, stack.Length - i);
							return;
						}
					}

					stack.Clear();
					stack.Add(data.Root);
				}

				void Return(ref BTData data, ref BTExec node)
				{
					Trace(ref node, BTExecTrace.Event.Return);

					stack.RemoveAt(stack.Length - 1);
				}

				void Call(ref BTData data, BTExecNodeId node)
				{
					Trace1(ref data, BTExecTrace.Event.Call);

					stack.ElementAt(stack.Length - 1).childIndex++;
					stack.Add(node);
				}

				switch(node.type)
				{
					case BTExec.BTExecType.Nop:
						Return(ref data, ref node);
						break;

					case BTExec.BTExecType.Root:
						if(stack.Length != 1)
							throw new Exception($"Root should always be the first stack frame, found at {stack.Length}");

						if(rootVisited)
						{
							// visit the root node at most once per frame to avoid getting stuck here
							Trace(ref node, BTExecTrace.Event.Yield);
							return;
						}

						rootVisited = true;

						Call(ref data, node.data.root.child);
						break;

					case BTExec.BTExecType.Sequence:
						if(stack[^1].childIndex < node.data.sequence.children.Length)
						{
							Call(ref data, node.data.sequence.children[stack[^1].childIndex]);
						}
						else
						{
							Return(ref data, ref node);
						}

						break;

					case BTExec.BTExecType.Selector:
						if(stack[^1].childIndex == 0)
						{
							bool any = false;

							for(int childIndex = 0; childIndex < node.data.selector.children.Length; ++childIndex)
							{
								ref var child = ref node.data.selector.children[childIndex];
								if(child.condition.Evaluate<bool>(in exprContext))
								{
									any = true;
									Call(ref data, child.nodeId);
									break;
								}
							}

							if(!any)
							{
								// none of the options worked
								Fail(ref data, ref node);
							}
						}
						else
						{
							// already executed one of our children, go back to parent
							Return(ref data, ref node);
						}
						break;

					case BTExec.BTExecType.WriteField:
						node.data.writeField.Evaluate(in exprContext);
						Return(ref data, ref node);
						break;

					case BTExec.BTExecType.Wait:
						if(node.data.wait.until.Evaluate<bool>(in exprContext))
						{
							Return(ref data, ref node);
						}
						else
						{
							// still waiting, can't execute any more nodes until input data changes
							Trace(ref node, BTExecTrace.Event.Wait);
							return;
						}

						break;

					case BTExec.BTExecType.Fail:
						Fail(ref data, ref node);
						break;

					case BTExec.BTExecType.Optional:
						if(stack[^1].childIndex == 0 && node.data.optional.condition.Evaluate<bool>(in exprContext))
						{
							Call(ref data, node.data.optional.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					case BTExec.BTExecType.Catch:
						if(stack[^1].childIndex == 0)
						{
							Call(ref data, node.data.@catch.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					default:
						throw new NotImplementedException($"BTExec node type {node.type} not implemented");
				}
			}
		}

		public static void DumpNodes(ref BTData data, List<string> output)
		{
			output.Add($"const data: {data.exprData.constData.Length} bytes");

			output.Add("");

			int j = 0;
			foreach(ref var exec in data.execs.AsSpan())
			{
				output.Add("Exec " + j.ToString() + ": " + exec.DumpString());
				j++;
			}

			output.Add("");

			j = 0;
			foreach(ref var expr in data.exprData.exprs.AsSpan())
			{
				output.Add("Expr " + j.ToString() + ": " + expr.DumpString());
			}
		}
	}
}
