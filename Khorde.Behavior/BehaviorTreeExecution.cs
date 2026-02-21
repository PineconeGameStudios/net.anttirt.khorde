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

		enum FailResult
		{
			Fail,
			Catch,
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
				Spawn(ref state, ref data, data.Root, -1, default, 0, -1);
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
				bool threadRootVisited = false;

				for(int cycle = 0; ; ++cycle)
				{
					if(cycle > 10000)
						throw new Exception("max cycle count exceeded; almost certainly a bug in the implementation");

					// NOTE: need to get these here because they may be invalidated from cycle to cycle
					ref var thread = ref threads.ElementAt(threadIndex);
					var threadId = thread.threadId;
					var frames = allFrames.AsNativeArray().GetSubArray(thread.frameOffset, thread.frameCount);

					var nodeId = frames[^1].nodeId;
					ref BTExec node = ref data.GetNode(nodeId);

					if(trace.IsCreated && cycle == 0)
						trace.Add(new(nodeId, node.type, BTExecTrace.Event.Start, threadId, frames.Length, cycle));

					if(cycle == 0)
					{
						switch(node.type)
						{
						case BTExec.BTExecType.Root:
						case BTExec.BTExecType.Wait:
						case BTExec.BTExecType.Query:
						case BTExec.BTExecType.ThreadRoot:
							break;

						default:
							throw new InvalidOperationException($"BUG: Execute() started with node type {node.type}");
						}
					}

					void Trace(ref BTExec node, BTExecTrace.Event @event)
					{
						if(trace.IsCreated)
							trace.Add(new(nodeId, node.type, @event, threadId, frames.Length, cycle));
					}

					void Trace1(ref BTData data, BTExecTrace.Event @event)
					{
						if(trace.IsCreated)
							trace.Add(new(nodeId, data.GetNode(nodeId).type, @event, threadId, frames.Length, cycle));
					}

					void Trace2(ref BTData data, int stackIndex, BTExecTrace.Event @event)
					{
						if(trace.IsCreated)
							trace.Add(new(frames[stackIndex].nodeId, data.GetNode(frames[stackIndex].nodeId).type, @event, threadId, stackIndex + 1, cycle));
					}

					FailResult Fail(ref BTState state, ref BTData data, ref BTExec node, ref BTThread thread)
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
								thread.frameCount -= count;
								return FailResult.Catch;
							}
						}

						int depth = frames.Length;

						//frames.Clear();
						//frames.Add(data.Root);

						// if nothing catches us, immediately abort all threads and start from scratch
						threads.Clear();
						allFrames.Clear();
						Spawn(ref state, ref data, data.Root, -1, nodeId, depth, cycle);
						return FailResult.Fail;
					}

					void Return(ref BTData data, ref BTExec node)
					{
						Trace(ref node, BTExecTrace.Event.Return);

						//frames.RemoveAt(frames.Length - 1);
						Pop(threadIndex);
					}

					void Call(ref BTData data, BTExecNodeId node)
					{
						Trace1(ref data, BTExecTrace.Event.Call);

						frames.ElementAt(frames.Length - 1).childIndex++;
						//frames.Add(node);
						Push(threadIndex, node);
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
							if(threadRootVisited)
							{
								// visit the thread root node at most once per frame to avoid getting stuck here
								Trace(ref node, BTExecTrace.Event.Yield);
								goto nextThread;
							}

							threadRootVisited = true;

							// thread start
							bool loop = node.data.threadRoot.loop;

							Call(ref data, node.data.threadRoot.child);

							if(loop)
							{
								frames.ElementAt(frames.Length - 2).childIndex--;
							}

							// NOTE: run more cycles to continue executing this
							// thread as far as it goes
						}
						else
						{
							// thread end
							Abort(ref state, ref data, threadIndex, threadIndex, nodeId, frames.Length, cycle);

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
								if(Fail(ref state, ref data, ref node, ref thread) == FailResult.Fail)
								{
									threadIndex = 0;
								}
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
							if(thread.waitStartTime == 0)
							{
								thread.waitStartTime = now;
							}

							float duration = node.data.wait.duration.Evaluate<float>(in exprContext);
							if(now - thread.waitStartTime >= duration)
							{
								thread.waitStartTime = 0;
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
						if(Fail(ref state, ref data, ref node, ref thread) == FailResult.Fail)
						{
							threadIndex = 0;
						}

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
							// Spawn invalidates local ref variables and
							// buffers, so we leave the Parallel as the current
							// frame, and run the Call in the second cycle
							frames.ElementAt(frames.Length - 1).childIndex++;
							Spawn(ref state, ref data, node.data.parallel.parallel, threadIndex, nodeId, frames.Length, cycle);
						}
						else if(frames[^1].childIndex == 1)
						{
							Call(ref data, node.data.parallel.main);
						}
						else
						{
							// end parallel, and end any threads owned by this
							// that may still be running
							for(int otherThreadIndex = 0; otherThreadIndex < threads.Length; ++otherThreadIndex)
							{
								if(otherThreadIndex != threadIndex && threads[otherThreadIndex].ownerThreadIndex == threadIndex)
								{
									Abort(ref state, ref data, otherThreadIndex, threadIndex, nodeId, frames.Length, cycle);
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

			/// <summary>
			/// Spawn a new thread of execution. Returns the index of the stack.
			/// </summary>
			/// <param name="threads"></param>
			/// <param name="frames"></param>
			/// <returns></returns>
			void Spawn(ref BTState state, ref BTData data, BTExecNodeId root, int ownerThreadIndex, BTExecNodeId caller, int depth, int cycle)
			{
				if(trace.IsCreated)
					trace.Add(new(caller, data.GetNode(caller).type, BTExecTrace.Event.Spawn, ownerThreadIndex == -1 ? 0 : threads[ownerThreadIndex].threadId, depth, cycle));

				var stack = new BTThread
				{
					frameCount = 0,
					frameOffset = 0,
					ownerThreadIndex = ownerThreadIndex,
					waitStartTime = float.NegativeInfinity,
					threadId = ownerThreadIndex == -1 ? 0 : ++state.threadIdCounter,
				};

				if(threads.Length > 0)
				{
					ref var last = ref threads.ElementAt(threads.Length - 1);
					stack.frameOffset = threads[^1].GetEndOffset();
				}

				threads.Add(stack);
				var threadIndex = threads.Length - 1;
				Push(threadIndex, root);
			}

			/// <summary>
			/// Abort a thread of execution and any descendant threads
			/// </summary>
			/// <param name="threads"></param>
			/// <param name="frames"></param>
			/// <param name="threadIndex"></param>
			void Abort(ref BTState btState, ref BTData data, int threadIndex, int callerThreadIndex, BTExecNodeId caller, int depth, int cycle)
			{
				if(trace.IsCreated)
					trace.Add(new(caller, data.GetNode(caller).type, BTExecTrace.Event.Abort, threads[callerThreadIndex].threadId, depth, cycle));

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
					Abort(ref btState, ref data, recursiveFinalizeIndex, callerThreadIndex, caller, depth, cycle);
				}
			}

			void Push(int threadIndex, BTExecNodeId node)
			{
				ref var stack = ref threads.ElementAt(threadIndex);

				if(Hint.Likely(threadIndex == threads.Length - 1))
				{
					if(stack.GetEndOffset() >= allFrames.Length)
						allFrames.Add(default);
				}
				else
				{
					ref var nextStack = ref threads.ElementAt(threadIndex + 1);
					if(Hint.Likely(stack.GetEndOffset() < nextStack.frameOffset))
					{
						allFrames[stack.frameCount] = default;
					}
					else
					{
						const int ShiftCount = 4;
						int moveCount = allFrames.Length - nextStack.frameOffset;
						int elemSize = UnsafeUtility.SizeOf<BTStackFrame>();
						allFrames.ResizeUninitialized(allFrames.Length + ShiftCount);

						unsafe
						{
							BTStackFrame* data = (BTStackFrame*)allFrames.GetUnsafePtr();
							BTStackFrame* src = data + nextStack.frameOffset;
							BTStackFrame* dst = src + ShiftCount;
							UnsafeUtility.MemMove(dst, src, moveCount * elemSize);
							UnsafeUtility.MemClear(src, ShiftCount * elemSize);
						}

						for(int nextStackIndex = threadIndex + 1; nextStackIndex < threads.Length; ++nextStackIndex)
							threads.ElementAt(nextStackIndex).frameOffset += ShiftCount;
					}
				}

				allFrames.ElementAt(stack.frameOffset + stack.frameCount++) = node;
			}

			int Pop(int threadIndex)
			{
				ref var stack = ref threads.ElementAt(threadIndex);
				stack.frameCount--;
				return stack.GetEndOffset() - 1;
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
