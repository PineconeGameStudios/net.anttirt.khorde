using Khorde.Blobs;
using Khorde.Entities;
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
			DynamicBuffer<BTInvokeQueue> invoke,
			EnabledRefRW<BTInvokeQueue> invokeEnabled,
			NativeArray<ExpressionBlackboardStorage> blackboard,
			ref ExpressionBlackboardLayout blackboardLayout,
			NativeArray<BlobAssetReference<QSData>> queries,
			EnabledRefRW<PendingQuery> pendingQueryEnabled,
			ref PendingQuery pendingQuery,
			NativeArray<UnsafeComponentReference> componentPtrs,
			NativeArray<UntypedComponentLookup> lookups,
			float now,
			float deltaTime,
			DynamicBuffer<BTExecTrace> trace)
			=> Execute(ref asset.Value, ref state, threads, frames, invoke, invokeEnabled, blackboard, ref blackboardLayout, queries, pendingQueryEnabled, ref pendingQuery, componentPtrs, lookups, now, deltaTime, trace);

		const float ThreadWaitStartTime_Invalid = -1;

		public static void Execute(
			ref BTData data,
			ref BTState state,
			DynamicBuffer<BTThread> threads,
			DynamicBuffer<BTStackFrame> allFrames,
			DynamicBuffer<BTInvokeQueue> invoke,
			EnabledRefRW<BTInvokeQueue> invokeEnabled,
			NativeArray<ExpressionBlackboardStorage> blackboard,
			ref ExpressionBlackboardLayout blackboardLayout,
			NativeArray<BlobAssetReference<QSData>> queries,
			EnabledRefRW<PendingQuery> pendingQueryEnabled,
			ref PendingQuery pendingQuery,
			NativeArray<UnsafeComponentReference> componentPtrs,
			NativeArray<UntypedComponentLookup> lookups,
			float now,
			float deltaTime,
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

			exprContext.time = now;
			exprContext.deltaTime = deltaTime;

			bool rootVisited = false;
			int cycle = -1;

			for(int threadIndex = 0; threadIndex < threads.Length; ++threadIndex)
			{
				bool threadRootVisited = false;
				int threadCycle = -1;

				while(true)
				{
					++cycle;
					++threadCycle;

					if(cycle > 10000)
					{
						UnityEngine.Debug.LogError("max cycle count exceeded; almost certainly a bug in the implementation");
						return;
					}

					// NOTE: need to get these here because they may be invalidated from cycle to cycle
					ref var thread = ref threads.ElementAt(threadIndex);
					var threadId = thread.threadId;
					var frames = allFrames.AsNativeArray().GetSubArray(thread.frameOffset, thread.frameCount);

					var nodeId = frames[^1].nodeId;
					ref BTExec node = ref data.GetNode(nodeId);

					if(trace.IsCreated && threadCycle == 0)
						trace.Add(new(nodeId, node.type, BTExecTrace.Event.Resume, threadId, frames.Length, cycle));

					if(threadCycle == 0)
					{
						switch(node.type)
						{
							case BTExec.BTExecType.Root:
							case BTExec.BTExecType.Wait:
							case BTExec.BTExecType.Query:
							case BTExec.BTExecType.ThreadRoot:
							case BTExec.BTExecType.Repeat:
							case BTExec.BTExecType.Invoke:
								break;

							default:
								throw new InvalidOperationException($"BUG: Execute() on thread {{{threadId}}} started with node type {node.type}");
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

					void Fail(ref BTState state, ref BTData data, ref BTExec node, ref BTThread thread)
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
								// resume from catch on the same thread on the next cycle
								return;
							}
							else if(stackNode.type == BTExec.BTExecType.Parallel)
							{
								// abort any child threads immediately
								for(int otherThreadIndex = 0; otherThreadIndex < threads.Length; ++otherThreadIndex)
								{
									if(otherThreadIndex != threadIndex && threads[otherThreadIndex].ownerThreadIndex == threadIndex)
									{
										Abort(ref state, ref data, otherThreadIndex, threadIndex, nodeId, frames.Length, cycle);
									}
								}
							}
						}

						int depth = frames.Length;

						// if this is a parallel branch, end the parallel branch immediately and fail the main branch instead
						if(thread.ownerThreadIndex != -1)
						{
							var ownerThreadIndex = thread.ownerThreadIndex;
							Abort(ref state, ref data, threadIndex, threadIndex, nodeId, frames.Length, cycle);

							// switch to owner thread and fail it instead
							threadIndex = ownerThreadIndex;
							thread = ref threads.ElementAt(threadIndex);
							threadId = thread.threadId;
							frames = allFrames.AsNativeArray().GetSubArray(thread.frameOffset, thread.frameCount);
							nodeId = frames[^1].nodeId;
							node = ref data.GetNode(nodeId);
							Fail(ref state, ref data, ref node, ref thread);
						}
						else
						{
							// if nothing catches us, immediately abort all threads and start from scratch
							threads.Clear();
							allFrames.Clear();
							threadIndex = 0;
							state.QueryExecutorThreadIndex = -1;
							Spawn(ref state, ref data, data.Root, -1, nodeId, depth, cycle);
						}
					}

					void Return(ref BTData data, ref BTExec node)
					{
						Trace(ref node, BTExecTrace.Event.Return);

						//frames.RemoveAt(frames.Length - 1);
						Pop(threadIndex);
					}

					void Call(ref BTData data, BTExecNodeId node, bool incrementChildIndex = true)
					{
						Trace1(ref data, BTExecTrace.Event.Call);

						if(incrementChildIndex)
							frames.UnsafeElementAt(frames.Length - 1).childIndex++;

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
								Call(ref data, node.data.threadRoot.child, incrementChildIndex: !node.data.threadRoot.loop);

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
									Fail(ref state, ref data, ref node, ref thread);
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

						case BTExec.BTExecType.WriteLookupField:
							if(node.data.writeLookupField.Evaluate(in exprContext))
							{
								Return(ref data, ref node);
							}
							else
							{
								Fail(ref state, ref data, ref node, ref thread);
							}
							break;

						case BTExec.BTExecType.Append:
							node.data.append.Evaluate(in exprContext);
							Return(ref data, ref node);
							break;

						case BTExec.BTExecType.WriteBufferField:
							if(node.data.writeBufferField.Evaluate(in exprContext))
							{
								Return(ref data, ref node);
							}
							else
							{
								Fail(ref state, ref data, ref node, ref thread);
							}
							break;

						case BTExec.BTExecType.Wait:
							{
								if(thread.waitStartTime == ThreadWaitStartTime_Invalid)
								{
									thread.waitStartTime = now;
								}

								bool done = node.data.wait.condition.Evaluate<bool>(in exprContext);

								if(node.data.wait.mode == Wait.ConditionMode.While)
									done = !done;

								if(done)
								{
									Return(ref data, ref node);
								}
								else if(node.data.wait.duration.IsCreated)
								{
									float duration = node.data.wait.duration.Evaluate<float>(in exprContext);
									if(now - thread.waitStartTime >= duration)
									{
										thread.waitStartTime = ThreadWaitStartTime_Invalid;
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
									// still waiting, can't execute any more nodes until input data changes
									Trace(ref node, BTExecTrace.Event.Wait);
									goto nextThread;
								}
							}

							break;

						case BTExec.BTExecType.Fail:
							Fail(ref state, ref data, ref node, ref thread);
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
								var varBytes = exprContext.GetBlackboardVariable(node.data.writeVar.variable);
								node.data.writeVar.input.Evaluate(exprContext, ref varBytes);
							}

							Return(ref data, ref node);
							break;

						case BTExec.BTExecType.Query:
							if(frames[^1].childIndex == 1)
							{
								Return(ref data, ref node);
								break;
							}
							else if(state.QueryExecutorThreadIndex == -1 || state.QueryExecutorThreadIndex == threadIndex)
							{
								if(!pendingQuery.complete && !pendingQueryEnabled.ValueRO)
								{
									// start query now
									pendingQueryEnabled.ValueRW = true;
									pendingQuery.query = queries[node.data.query.queryIndex];
									pendingQuery.results = exprContext.GetBlackboardVariableSlice(node.data.query.result);
									state.QueryExecutorThreadIndex = threadIndex;

									for(int i = 0; i < node.data.query.inputs.Length; ++i)
									{
										ref var writeVar = ref node.data.query.inputs[i];
										var varBytes = exprContext.GetBlackboardVariable(writeVar.variable);
										writeVar.input.Evaluate(exprContext, ref varBytes);
									}

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
									exprContext.GetBlackboardVariable(node.data.query.resultCount).ReinterpretStore(0, pendingQuery.resultCount);

									// allow other threads to run queries again
									state.QueryExecutorThreadIndex = -1;

									if(pendingQuery.resultCount > 0)
									{
										Call(ref data, node.data.query.success);
									}
									else if(node.data.query.retry)
									{
										// retry on the next iteration, but yield for one frame so we don't starve
										// a parallel branch that might also want to run a query

										// TODO: need fancier scheduling if we
										// want to support more than 2 threads
										// competing to run queries

										Trace(ref node, BTExecTrace.Event.Yield);
										frames.UnsafeElementAt(frames.Length - 1).childIndex = 0;
										goto nextThread;
									}
									else
									{
										Call(ref data, node.data.query.failure);
									}

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
								frames.UnsafeElementAt(frames.Length - 1).childIndex++;
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

						case BTExec.BTExecType.Repeat:
							{
								var counter = exprContext.GetBlackboardVariable(node.data.repeat.counter).Reinterpret<int>(1);

								bool enter = frames[^1].childIndex == 0;

								if(enter)
								{
									counter.UnsafeElementAt(0) = 0;
									frames.UnsafeElementAt(frames.Length - 1).childIndex = 1;
								}

								switch(node.data.repeat.mode)
								{
									case RepeatMode.Count:
										int repeatCount = node.data.repeat.param.Evaluate<int>(exprContext);

										if(!enter)
											counter.UnsafeElementAt(0)++;

										if(counter[0] < repeatCount)
											Call(ref data, node.data.repeat.child, incrementChildIndex: false);
										else
											Return(ref data, ref node);

										break;

									case RepeatMode.Infinite:
									case RepeatMode.Condition:
										if(node.data.repeat.mode == RepeatMode.Infinite || node.data.repeat.param.Evaluate<bool>(exprContext))
										{
											// in infinite/condition mode, run only one iteration per frame to
											// avoid getting stuck in this loop
											if(frames[^1].childIndex == 2)
											{
												Trace(ref node, BTExecTrace.Event.Yield);
												frames.UnsafeElementAt(frames.Length - 1).childIndex = 1;
												goto nextThread;
											}
											else
											{
												if(!enter)
													counter.UnsafeElementAt(0)++;

												frames.UnsafeElementAt(frames.Length - 1).childIndex = 2;

												Call(ref data, node.data.repeat.child, incrementChildIndex: false);
											}
										}
										else
										{
											Return(ref data, ref node);
										}

										break;

									default:
										break;
								}
							}

							break;

						case BTExec.BTExecType.Invoke:
							{
								if(frames[^1].childIndex == 0)
								{
									// this will be picked up by BehaviorTreeActionSystem
									invoke.Add(new BTInvokeQueue { actionIndex = node.data.invoke.actionIndex });
									invokeEnabled.ValueRW = true;

									if(node.data.invoke.blocking)
									{
										// blocking: the action system runs the action on the next frame;
										// wait until that's done
										Trace(ref node, BTExecTrace.Event.Yield);
										frames.UnsafeElementAt(frames.Length - 1).childIndex = 1;
										goto nextThread;
									}
									else
									{
										// non-blocking: just resume immediately, allowing queueing up
										// multiple actions on the same frame, etc.
										Return(ref data, ref node);
									}
								}
								else
								{
									Return(ref data, ref node);
								}
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
					waitStartTime = ThreadWaitStartTime_Invalid,
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
