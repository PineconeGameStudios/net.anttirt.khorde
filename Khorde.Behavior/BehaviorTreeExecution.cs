using Khorde.Blobs;
using Khorde.Expr;
using Khorde.Query;
using System;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Khorde.Behavior
{
	public static class BehaviorTreeExecution
	{
		public static void Execute(
			this BlobAssetReference<BTData> asset,
			ref BTState state,
			DynamicBuffer<BTThread> threads,
			DynamicBuffer<BTStackFrame> frames,
			NativeArray<ExpressionBlackboardStorage> blackboard,
			ref ExpressionBlackboardLayout blackboardLayout,
			NativeArray<UnityObjectRef<QueryGraphAsset>> queries,
			EnabledRefRW<PendingQuery> pendingQueryEnabled,
			ref PendingQuery pendingQuery,
			NativeArray<UnsafeComponentReference> componentPtrs,
			NativeArray<UntypedComponentLookup> lookups,
			float now,
			DynamicBuffer<BTExecTrace> trace)
			=> Execute(ref asset.Value, ref state, threads, frames, blackboard, ref blackboardLayout, queries, pendingQueryEnabled, ref pendingQuery, componentPtrs, lookups, now, trace);

		/// <summary>
		/// Spawn a new thread of execution. Returns the index of the stack.
		/// </summary>
		/// <param name="threads"></param>
		/// <param name="frames"></param>
		/// <returns></returns>
		public static void Spawn(DynamicBuffer<BTThread> threads, DynamicBuffer<BTStackFrame> frames, BTExecNodeId root, int ownerThreadIndex)
		{
			UnityEngine.Debug.Log($"Spawn(node: {root.index}) -> thread #{threads.Length}");

			var stack = new BTThread
			{
				frameCount = 0,
				frameOffset = 0,
				ownerThreadIndex = ownerThreadIndex,
				waitStartTime = float.NegativeInfinity,
			};

			if(threads.Length > 0)
			{
				ref var last = ref threads.ElementAt(threads.Length - 1);
				stack.frameOffset = threads[^1].GetEndOffset();
			}

			threads.Add(stack);
			var stackIndex = threads.Length - 1;
			Push(threads, frames, stackIndex, root);
		}

		/// <summary>
		/// Abort a thread of execution and any descendant threads
		/// </summary>
		/// <param name="threads"></param>
		/// <param name="frames"></param>
		/// <param name="threadIndex"></param>
		public static void Abort(ref BTState btState, DynamicBuffer<BTThread> threads, DynamicBuffer<BTStackFrame> frames, int threadIndex)
		{
			UnityEngine.Debug.Log($"Abort(thread: {threadIndex})");

			// remove stack, shifting later threads down
			threads.RemoveAt(threadIndex);

			// fix up and clean up locks
			if(btState.QueryExecutorThreadIndex == threadIndex)
			{
				btState.QueryExecutorThreadIndex = -1;
			}
			else if(btState.QueryExecutorThreadIndex > threadIndex)
			{
				--btState.QueryExecutorThreadIndex;
			}

			// discover a descendant thread if there is one
			int recursiveFinalizeIndex = -1;

			for(int i = 0; i < threads.Length; ++i)
			{
				ref var stack = ref threads.ElementAt(i);

				if(stack.ownerThreadIndex == threadIndex)
				{
					// descendant of current, needs to also be finalized immediately
					recursiveFinalizeIndex = i;
				}
				else if(stack.ownerThreadIndex > threadIndex)
				{
					// the owner was shifted down
					stack.ownerThreadIndex--;
				}
			}

			if(recursiveFinalizeIndex != -1)
			{
				// finalize descendant thread
				Abort(ref btState, threads, frames, recursiveFinalizeIndex);
			}
		}

		/// <summary>
		/// Push a new stack frame onto the stack of a given thread. Returns the index of the pushed stack frame.
		/// </summary>
		/// <param name="threads"></param>
		/// <param name="frames"></param>
		/// <param name="stackIndex"></param>
		/// <returns></returns>
		public static void Push(DynamicBuffer<BTThread> threads, DynamicBuffer<BTStackFrame> frames, int stackIndex, BTExecNodeId node)
		{
			ref var stack = ref threads.ElementAt(stackIndex);

			if(Hint.Likely(stackIndex == threads.Length - 1))
			{
				if(stack.GetEndOffset() >= frames.Length)
					frames.Add(default);
			}
			else
			{
				ref var nextStack = ref threads.ElementAt(stackIndex + 1);
				if(Hint.Likely(stack.GetEndOffset() < nextStack.frameOffset))
				{
					frames[stack.frameCount] = default;
				}
				else
				{
					const int ShiftCount = 4;
					int moveCount = frames.Length - nextStack.frameOffset;
					int elemSize = UnsafeUtility.SizeOf<BTStackFrame>();
					frames.ResizeUninitialized(frames.Length + ShiftCount);

					unsafe
					{
						BTStackFrame* src = (BTStackFrame*)frames.GetUnsafePtr();
						BTStackFrame* dst = src + ShiftCount;
						UnsafeUtility.MemMove(dst, src, moveCount * elemSize);
						UnsafeUtility.MemClear(src, ShiftCount * elemSize);
					}

					for(int nextStackIndex = stackIndex + 1; nextStackIndex < threads.Length; ++nextStackIndex)
						threads.ElementAt(nextStackIndex).frameOffset += ShiftCount;
				}
			}

			frames.ElementAt(stack.frameOffset + stack.frameCount++) = node;
		}

		public static int Pop(DynamicBuffer<BTThread> threads, DynamicBuffer<BTStackFrame> frames, int threadIndex)
		{
			ref var stack = ref threads.ElementAt(threadIndex);
			stack.frameCount--;
			return stack.GetEndOffset() - 1;
		}

		public static void Execute(
			ref BTData data,
			ref BTState state,
			DynamicBuffer<BTThread> threads,
			DynamicBuffer<BTStackFrame> allFrames,
			NativeArray<ExpressionBlackboardStorage> blackboard,
			ref ExpressionBlackboardLayout blackboardLayout,
			NativeArray<UnityObjectRef<QueryGraphAsset>> queries,
			EnabledRefRW<PendingQuery> pendingQueryEnabled,
			ref PendingQuery pendingQuery,
			NativeArray<UnsafeComponentReference> componentPtrs,
			NativeArray<UntypedComponentLookup> lookups,
			float now,
			DynamicBuffer<BTExecTrace> trace)
		{
			data.exprData.CheckExpressionComponents(componentPtrs, lookups);

			if(Hint.Unlikely(threads.Length == 0))
			{
				if(trace.IsCreated)
					trace.Add(new(data.Root, BTExec.BTExecType.Root, BTExecTrace.Event.Init, allFrames.Length, -1));

				Spawn(threads, allFrames, data.Root, 0);
			}

			NativeArray<byte> blackboardBytes = default;
			if(blackboard.IsCreated)
			{
				blackboardBytes = blackboard.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>());
			}

			var exprContext = new ExpressionEvalContext(ref data.exprData, componentPtrs, lookups, blackboardBytes,
				ref blackboardLayout);

			bool rootVisited = false;

			for(int threadIndex = 0; threadIndex < threads.Length; ++threadIndex)
			{
				for(int cycle = 0; ; ++cycle)
				{
					if(cycle > 10000)
						throw new Exception("max cycle count exceeded; almost certainly a bug in the implementation");

					var stack = threads[threadIndex];

					var frames = allFrames.AsNativeArray().GetSubArray(stack.frameOffset, stack.frameCount);

					var nodeId = frames[^1].nodeId;

					ref BTExec node = ref data.GetNode(nodeId);

					if(trace.IsCreated && cycle == 0)
						trace.Add(new(nodeId, node.type, BTExecTrace.Event.Start, frames.Length, cycle));

					if(cycle == 0 && node.type != BTExec.BTExecType.Root && node.type != BTExec.BTExecType.Wait && node.type != BTExec.BTExecType.Query)
						throw new InvalidOperationException($"BUG: Execute() started with node type {node.type}");

					void Trace(ref BTExec node, BTExecTrace.Event @event)
					{
						if(trace.IsCreated)
							trace.Add(new(nodeId, node.type, @event, frames.Length, cycle));
					}

					void Trace1(ref BTData data, BTExecTrace.Event @event)
					{
						if(trace.IsCreated)
							trace.Add(new(nodeId, data.GetNode(nodeId).type, @event, frames.Length, cycle));
					}

					void Trace2(ref BTData data, int stackIndex, BTExecTrace.Event @event)
					{
						if(trace.IsCreated)
							trace.Add(new(frames[stackIndex].nodeId, data.GetNode(frames[stackIndex].nodeId).type, @event, stackIndex + 1, cycle));
					}

					void Fail(ref BTData data, ref BTExec node)
					{
						Trace(ref node, BTExecTrace.Event.Fail);

						for(int i = frames.Length - 1; i > 0; --i)
						{
							ref var stackNode = ref data.GetNode(frames[i].nodeId);
							if(stackNode.type == BTExec.BTExecType.Catch)
							{
								Trace2(ref data, i, BTExecTrace.Event.Catch);
								var count = frames.Length - i;
								//frames.RemoveRange(i, count);
								stack.frameCount -= count;
								return;
							}
						}

						//frames.Clear();
						//frames.Add(data.Root);

						// if nothing catches us, immediately abort all threads and start from scratch
						threads.Clear();
						allFrames.Clear();
					}

					void Return(ref BTData data, ref BTExec node)
					{
						Trace(ref node, BTExecTrace.Event.Return);

						//frames.RemoveAt(frames.Length - 1);
						Pop(threads, allFrames, threadIndex);
					}

					void Call(ref BTData data, BTExecNodeId node)
					{
						Trace1(ref data, BTExecTrace.Event.Call);

						frames.ElementAt(frames.Length - 1).childIndex++;
						//frames.Add(node);
						Push(threads, allFrames, threadIndex, node);
					}

					switch(node.type)
					{
					case BTExec.BTExecType.Nop:
						Return(ref data, ref node);
						break;

					case BTExec.BTExecType.Root:
						if(frames.Length != 1)
							throw new Exception($"Root should always be the first stack frame, found at {frames.Length}");

						if(rootVisited)
						{
							// visit the root node at most once per frame to avoid getting stuck here
							Trace(ref node, BTExecTrace.Event.Yield);
							goto nextThread;
						}

						rootVisited = true;

						Call(ref data, node.data.root.child);
						break;

					case BTExec.BTExecType.ThreadRoot:
						if(frames.Length != 1)
							throw new Exception($"Root should always be the first stack frame, found at {frames.Length}");

						if(frames[^1].childIndex == 0)
						{
							// thread start
							Call(ref data, node.data.root.child);

							// NOTE: run more cycles to continue executing this
							// thread as far as it goes
						}
						else
						{
							// thread end
							Abort(ref state, threads, allFrames, threadIndex);

							// this index was removed, so loop it again
							--threadIndex;

							goto nextThread;
						}

						break;

					case BTExec.BTExecType.Sequence:
						if(frames[^1].childIndex < node.data.sequence.children.Length)
						{
							Call(ref data, node.data.sequence.children[frames[^1].childIndex]);
						}
						else
						{
							Return(ref data, ref node);
						}

						break;

					case BTExec.BTExecType.Selector:
						if(frames[^1].childIndex == 0)
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
						if(node.data.wait.duration.IsCreated)
						{
							if(stack.waitStartTime == 0)
							{
								stack.waitStartTime = now;
							}

							float duration = node.data.wait.duration.Evaluate<float>(in exprContext);
							if(now - stack.waitStartTime >= duration)
							{
								stack.waitStartTime = 0;
								Return(ref data, ref node);
							}
							else
							{
								// still waiting, can't execute any more nodes until more time elapses
								Trace(ref node, BTExecTrace.Event.Wait);
								goto nextThread;
							}
						}
						else
						{
							if(node.data.wait.until.Evaluate<bool>(in exprContext))
							{
								Return(ref data, ref node);
							}
							else
							{
								// still waiting, can't execute any more nodes until input data changes
								Trace(ref node, BTExecTrace.Event.Wait);
								goto nextThread;
							}
						}

						break;

					case BTExec.BTExecType.Fail:
						Fail(ref data, ref node);
						break;

					case BTExec.BTExecType.Optional:
						if(frames[^1].childIndex == 0 && node.data.optional.condition.Evaluate<bool>(in exprContext))
						{
							Call(ref data, node.data.optional.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					case BTExec.BTExecType.Catch:
						if(frames[^1].childIndex == 0)
						{
							Call(ref data, node.data.@catch.child);
						}
						else
						{
							Return(ref data, ref node);
						}
						break;

					case BTExec.BTExecType.WriteVar:
						{
							var varBytes = exprContext.GetBlackboardVariable(node.data.writeVar.variableIndex);
							node.data.writeVar.input.Evaluate(exprContext, ref varBytes);
						}

						Return(ref data, ref node);
						break;

					case BTExec.BTExecType.Query:
						if(state.QueryExecutorThreadIndex == -1 || state.QueryExecutorThreadIndex == threadIndex)
						{
							if(!pendingQuery.complete && !pendingQueryEnabled.ValueRO)
							{
								// start query now
								pendingQueryEnabled.ValueRW = true;
								pendingQuery.query = queries[node.data.query.queryIndex];
								pendingQuery.results = exprContext.GetBlackboardVariableSlice(node.data.query.variableIndex);
								state.QueryExecutorThreadIndex = threadIndex;
								Trace(ref node, BTExecTrace.Event.Wait);
								goto nextThread;
							}
							else if(pendingQueryEnabled.ValueRO)
							{
								// query still running, can't execute any more nodes until input data changes
								Trace(ref node, BTExecTrace.Event.Wait);
								goto nextThread;
							}
							else
							{
								// query finished running

								// allow a new query to start the next time a Query node is reached
								pendingQuery.complete = false;

								// TODO: this would be an excellent moment to write the result count somewhere
								// on the bt execution stack, but a blackboard variable will do for now
								exprContext.GetBlackboardVariable(node.data.query.resultCountVariableIndex).ReinterpretStore(0, pendingQuery.resultCount);
								Return(ref data, ref node);

								// allow other threads to run queries again
								state.QueryExecutorThreadIndex = -1;
								break;
							}
						}
						else
						{
							// another thread is running a query, need to wait for it to be complete
							Trace(ref node, BTExecTrace.Event.Wait);
							goto nextThread;
						}

					case BTExec.BTExecType.Parallel:
						if(frames[^1].childIndex == 0)
						{
							Call(ref data, node.data.parallel.main);

							// start a second thread of execution
							Spawn(threads, allFrames, node.data.parallel.parallel, threadIndex);
						}
						else
						{
							// end parallel, and end any threads owned by this
							// that may still be running
							for(int otherThreadIndex = 0; otherThreadIndex < threads.Length; ++otherThreadIndex)
							{
								if(otherThreadIndex != threadIndex && threads[otherThreadIndex].ownerThreadIndex == threadIndex)
								{
									Abort(ref state, threads, allFrames, otherThreadIndex);
								}
							}

							Return(ref data, ref node);
						}

						break;

					default:
						throw new NotImplementedException($"BTExec node type {node.type} not implemented");
					}
				}

				nextThread:
				;
			}
		}

		public static void DumpNodes(ref BTData data, List<string> output)
		{
			output.Add($"const data: {data.exprData.constants.Length} bytes");

			output.Add("");

			int j = 0;
			foreach(ref var exec in data.execs.AsSpan())
			{
				output.Add("Exec " + j.ToString() + ": " + exec.DumpString());
				j++;
			}

			output.Add("");

			j = 0;
			foreach(ref var expr in data.exprData.expressions.AsSpan())
			{
				output.Add("Expr " + j.ToString() + ": (TODO)");// + expr.DumpString());
			}
		}
	}
}
