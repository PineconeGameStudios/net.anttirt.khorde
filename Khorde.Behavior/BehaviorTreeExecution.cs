using Khorde.Blobs;
using Khorde.Entities;
using Khorde.Expr;
using Khorde.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Khorde.Behavior
{
	public static class BehaviorTreeExecution
	{
		public static void Execute(
			this BlobAssetReference<BTData> asset,
			ref BTState state,
			DynamicBuffer<BTThread> threads,
			DynamicBuffer<BTStackFrame> frames,
			DynamicBuffer<BehaviorTreeInvocation> invoke,
			EnabledRefRW<BehaviorTreeInvocation> invokeEnabled,
			NativeArray<ExpressionBlackboardStorage> blackboard,
			ref ExpressionBlackboardLayout blackboardLayout,
			NativeArray<BlobAssetReference<QSData>> queries,
			EnabledRefRW<PendingQuery> pendingQueryEnabled,
			ref PendingQuery pendingQuery,
			NativeArray<UnsafeComponentReference> componentPtrs,
			NativeArray<UntypedComponentLookup> lookups,
			float now,
			float deltaTime,
			DynamicBuffer<BTExecTrace> trace
#if UNITY_EDITOR
			, DynamicBuffer<BTUtilityDebug> utilityDebug = default
#endif
			)
			=> Execute(
				ref asset.Value,
				ref state,
				threads,
				frames,
				invoke,
				invokeEnabled,
				blackboard,
				ref blackboardLayout,
				queries,
				pendingQueryEnabled,
				ref pendingQuery,
				componentPtrs,
				lookups,
				now,
				deltaTime,
				trace
#if UNITY_EDITOR
				, utilityDebug
#endif
				);

		public const float ThreadWaitStartTime_Invalid = -1;

		public static void Execute(
			ref BTData data,
			ref BTState state,
			DynamicBuffer<BTThread> threads,
			DynamicBuffer<BTStackFrame> allFrames,
			DynamicBuffer<BehaviorTreeInvocation> invoke,
			EnabledRefRW<BehaviorTreeInvocation> invokeEnabled,
			NativeArray<ExpressionBlackboardStorage> blackboard,
			ref ExpressionBlackboardLayout blackboardLayout,
			NativeArray<BlobAssetReference<QSData>> queries,
			EnabledRefRW<PendingQuery> pendingQueryEnabled,
			ref PendingQuery pendingQuery,
			NativeArray<UnsafeComponentReference> componentPtrs,
			NativeArray<UntypedComponentLookup> lookups,
			float now,
			float deltaTime,
			DynamicBuffer<BTExecTrace> trace
#if UNITY_EDITOR
			, DynamicBuffer<BTUtilityDebug> utilityDebug = default
#endif
			)
		{
			data.exprData.CheckExpressionComponents(componentPtrs, lookups);

			if(Hint.Unlikely(threads.Length == 0))
			{
				Spawn(ref state, ref data, data.Root, -1, default, 0, -1);
				if(state.random.state == 0)
					state.random = Random.CreateFromIndex((uint)now);

#if UNITY_EDITOR
				if(utilityDebug.IsCreated && utilityDebug.Length != data.execs.Length)
					utilityDebug.Resize(data.execs.Length, NativeArrayOptions.ClearMemory);
#endif
			}

			NativeArray<byte> blackboardBytes = default;
			if(blackboard.IsCreated)
			{
				blackboardBytes = blackboard.Reinterpret<byte>(UnsafeUtility.SizeOf<ExpressionBlackboardStorage>());
			}

			NativeList<float> utilities = new(Allocator.Temp);

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
							case BTExec.BTExecType.UtilitySelector:
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
								frames.ElementAt(frames.Length - 1).execOutputIndex = frames[^1].childIndex;
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
										frames.ElementAt(frames.Length - 1).execOutputIndex = (byte)childIndex;
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
									thread.waitDuration = duration;
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

						case BTExec.BTExecType.If:
							if(frames[^1].childIndex == 0)
							{
								if(node.data.@if.condition.Evaluate<bool>(in exprContext))
								{
									frames.ElementAt(frames.Length - 1).execOutputIndex = 0;
									Call(ref data, node.data.@if.then);
								}
								else
								{
									frames.ElementAt(frames.Length - 1).execOutputIndex = 1;
									Call(ref data, node.data.@if.@else);
								}
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
								ref var query = ref node.data.query;

								if(!pendingQuery.complete && !pendingQueryEnabled.ValueRO)
								{
									// start query now
									pendingQueryEnabled.ValueRW = true;
									pendingQuery.query = queries[query.queryIndex];
									pendingQuery.results = exprContext.GetBlackboardVariableSlice(query.result);
									state.QueryExecutorThreadIndex = threadIndex;

									for(int i = 0; i < query.inputs.Length; ++i)
									{
										ref var writeVar = ref query.inputs[i];
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

									// store the result on the blackboard so downstream nodes can use it
									exprContext.GetBlackboardVariable(query.resultCount).ReinterpretStore(0, pendingQuery.resultCount);

									// allow other threads to run queries again
									state.QueryExecutorThreadIndex = -1;

									if(pendingQuery.resultCount > 0)
									{
										Call(ref data, query.success);
									}
									else if(query.retry)
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
										frames.ElementAt(frames.Length - 1).execOutputIndex = 1;
										Call(ref data, query.failure);
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
									invoke.Add(new BehaviorTreeInvocation
									{
										actionIndex = node.data.invoke.actionIndex,
									});

									var storage = invoke.ElementAt(invoke.Length - 1).UnsafeGetTempStorageArray();
									ref var invokeParams = ref node.data.invoke.parameters;
									for(int i = 0; i < invokeParams.Length; ++i)
									{
										ref var invokeParam = ref invokeParams[i];
										var slice = storage.GetSubArray(invokeParam.offset, invokeParam.size);
										invokeParam.expr.Evaluate(in exprContext, ref slice);
									}

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

						case BTExec.BTExecType.UtilitySelector:
							{
								int childIndex = frames[^1].childIndex;

								if(childIndex >= node.data.utilitySelector.queries.Length + 1)
								{
									// done with this selector
									Return(ref data, ref node);
									break;
								}

								// may need to pre-evaluate some downstream queries to compute utility
								while(childIndex < node.data.utilitySelector.queries.Length)
								{
									if(state.QueryExecutorThreadIndex == -1 || state.QueryExecutorThreadIndex == threadIndex)
									{
										ref var queryNode = ref data.execs[node.data.utilitySelector.queries[childIndex].index];
										CheckQueryNode(ref queryNode);
										ref var query = ref queryNode.data.query;

										if(!pendingQuery.complete && !pendingQueryEnabled.ValueRO)
										{
											// start query now
											pendingQueryEnabled.ValueRW = true;
											pendingQuery.query = queries[query.queryIndex];
											pendingQuery.results = exprContext.GetBlackboardVariableSlice(query.result);
											state.QueryExecutorThreadIndex = threadIndex;

											for(int i = 0; i < query.inputs.Length; ++i)
											{
												ref var writeVar = ref query.inputs[i];
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

											// store the result count on the blackboard so utility computation can use it
											exprContext.GetBlackboardVariable(query.resultCount).ReinterpretStore(0, pendingQuery.resultCount);

											// allow other threads to run queries again
											state.QueryExecutorThreadIndex = -1;

											childIndex = ++frames.UnsafeElementAt(frames.Length - 1).childIndex;
										}
									}
									else
									{
										// another thread is running a query, need to wait for it to complete
										Trace(ref node, BTExecTrace.Event.Wait);
										goto nextThread;
									}
								}

								// then, compute utility and execute the selected child, or fail if none were valid
								utilities.Clear();

								int actionCount = node.data.utilitySelector.actions.Length;
								utilities.ResizeUninitialized(actionCount);
								float maxUtility = 0;

								for(int i = 0; i < actionCount; ++i)
								{
									utilities[i] = GetUtility(ref state, ref data, in exprContext, node.data.utilitySelector.actions[i].nodeId
#if UNITY_EDITOR
										, utilityDebug
#endif
										);
									maxUtility = math.max(maxUtility, utilities[i]);
								}

								float minValid = maxUtility - node.data.utilitySelector.tieTolerance;

								int index = -1;

								if(maxUtility > 0)
								{
									if(node.data.utilitySelector.tieBreakMode == UtilitySelector.TieBreakMode.Top)
									{
										// select first passing
										for(int i = 0; i < actionCount; ++i)
										{
											if(utilities[i] >= minValid)
											{
												index = i;
												break;
											}
										}
									}
									else
									{
										// select random passing
										int r = 0;

										for(int i = 0; i < actionCount; ++i)
										{
											if(utilities[i] >= minValid)
											{
												if(r == 0 || state.random.NextInt(0, r + 1) == 0)
													index = i;

												r++;
											}
										}
									}
								}

								if(index != -1)
								{
									frames.ElementAt(frames.Length - 1).execOutputIndex = (byte)index;
									Call(ref data, node.data.utilitySelector.actions[index].nodeId);
								}
								else
								{
									// all actions had utility 0 (e.g. everything is on cooldown)
									Fail(ref state, ref data, ref node, ref thread);
								}
							}
							break;

						case BTExec.BTExecType.Utility:
							if(frames[^1].childIndex == 0)
								Call(ref data, node.data.utility.child);
							else
								Return(ref data, ref node);
							break;

						case BTExec.BTExecType.UtilityCooldown:
							if(frames[^1].childIndex == 0)
							{
								ref var nd = ref node.data.utilityCooldown;
								exprContext.GetBlackboardVariable<float>(nd.lastExecutionTime) = now;
								Call(ref data, node.data.utilityCooldown.child);
							}
							else
							{
								Return(ref data, ref node);
							}
							break;

						case BTExec.BTExecType.UtilityCurve:
							if(frames[^1].childIndex == 0)
								Call(ref data, node.data.utilityCurve.child);
							else
								Return(ref data, ref node);
							break;

						case BTExec.BTExecType.UtilityRange:
							if(frames[^1].childIndex == 0)
								Call(ref data, node.data.utilityRange.child);
							else
								Return(ref data, ref node);
							break;

						default:
							throw new NotImplementedException($"BTExec node type {node.type} not implemented");
					}
				}

			nextThread:
				;
			}

			float GetUtility(ref BTState state, ref BTData data, in ExpressionEvalContext exprContext, BTExecNodeId nodeId
#if UNITY_EDITOR
				, DynamicBuffer<BTUtilityDebug> utilityDebug
#endif
				)
			{
				ref var node = ref data.GetNode(nodeId);

				if((node.flags & BTExec.Flags.ZeroUtility) != 0)
				{
#if UNITY_EDITOR
					if(utilityDebug.IsCreated)
						utilityDebug.ElementAt(nodeId.index).value = 0;
#endif
					return 0;
				}

				float result;

				// TODO: most nodes don't need recursion so this could be partially loopified

				switch(node.type)
				{
					case BTExec.BTExecType.Nop: return 0;

					case BTExec.BTExecType.Root:
						result = GetUtility(ref state, ref data, in exprContext, node.data.root.child
#if UNITY_EDITOR
							, utilityDebug
#endif
							);
						break;

					case BTExec.BTExecType.Sequence:
						{
							float max = 0;
							for(int i = 0; i < node.data.sequence.children.Length; ++i)
								max = math.max(max, GetUtility(ref state, ref data, in exprContext, node.data.sequence.children[i]
#if UNITY_EDITOR
									, utilityDebug
#endif
									));
							result = max;
							break;
						}

					case BTExec.BTExecType.Selector:
						{
							// TODO: define selection probability?
							float max = 0;
							for(int i = 0; i < node.data.selector.children.Length; ++i)
								max = math.max(max, GetUtility(ref state, ref data, in exprContext, node.data.selector.children[i].nodeId
#if UNITY_EDITOR
									, utilityDebug
#endif
									));
							result = max;
							break;
						}

					case BTExec.BTExecType.WriteField: result = 0; break;
					case BTExec.BTExecType.Wait: result = 0; break;
					case BTExec.BTExecType.Fail: result = 0; break;

					case BTExec.BTExecType.If:
						// TODO: evaluate the condition? use a probability for each branch?
						result = math.max(
							GetUtility(ref state, ref data, in exprContext, node.data.@if.then
#if UNITY_EDITOR
								, utilityDebug
#endif
							),
							GetUtility(ref state, ref data, in exprContext, node.data.@if.@else
#if UNITY_EDITOR
								, utilityDebug
#endif
							)
							);
						break;

					case BTExec.BTExecType.Catch:
						result = GetUtility(ref state, ref data, in exprContext, node.data.@catch.child
#if UNITY_EDITOR
							, utilityDebug
#endif
							);
						break;

					case BTExec.BTExecType.WriteVar: result = 0; break;

					case BTExec.BTExecType.Query:
						// the query was evaluated at the start of the UtilitySelector so we have results here
						if(exprContext.GetBlackboardVariable<int>(node.data.query.resultCount) > 0)
							return GetUtility(ref state, ref data, in exprContext, node.data.query.success
#if UNITY_EDITOR
							, utilityDebug
#endif
							);
						else
							return GetUtility(ref state, ref data, in exprContext, node.data.query.failure
#if UNITY_EDITOR
							, utilityDebug
#endif
							);

					case BTExec.BTExecType.Parallel:
						return
							math.max(
								GetUtility(ref state, ref data, in exprContext, node.data.parallel.main
#if UNITY_EDITOR
								, utilityDebug
#endif
								),
								GetUtility(ref state, ref data, in exprContext, node.data.parallel.parallel
#if UNITY_EDITOR
								, utilityDebug
#endif
								)
							);

					case BTExec.BTExecType.ThreadRoot:
						result = GetUtility(ref state, ref data, in exprContext, node.data.threadRoot.child
#if UNITY_EDITOR
							, utilityDebug
#endif
							);
						break;

					case BTExec.BTExecType.Repeat:
						result = GetUtility(ref state, ref data, in exprContext, node.data.repeat.child
#if UNITY_EDITOR
							, utilityDebug
#endif
							);
						break;

					case BTExec.BTExecType.Append: result = 0; break;
					case BTExec.BTExecType.Invoke: result = 0; break;
					case BTExec.BTExecType.WriteBufferField: result = 0; break;
					case BTExec.BTExecType.WriteLookupField: result = 0; break;

					case BTExec.BTExecType.UtilitySelector:
						{
							float max = 0;
							for(int i = 0; i < node.data.utilitySelector.actions.Length; ++i)
								max = math.max(max, GetUtility(ref state, ref data, in exprContext, node.data.utilitySelector.actions[i].nodeId
#if UNITY_EDITOR
									, utilityDebug
#endif
									));
							result = max;
							break;
						}

					case BTExec.BTExecType.Utility:
						result = node.data.utility.input.Evaluate<float>(in exprContext);
						break;

					case BTExec.BTExecType.UtilityCooldown:
						{
							ref var uc = ref node.data.utilityCooldown;
							var input = GetUtility(ref state, ref data, in exprContext, uc.child
#if UNITY_EDITOR
								, utilityDebug
#endif
								);
							var lastExecutionTime = exprContext.GetBlackboardVariable<float>(uc.lastExecutionTime);
							var t = math.saturate((now - lastExecutionTime) * uc.invDuration);
							result = uc.curve.Evaluate(t) * input;
							break;
						}

					case BTExec.BTExecType.UtilityCurve:
						{
							ref var nd = ref node.data.utilityCurve;
							float input;
							if(nd.customInput)
								input = nd.input.Evaluate<float>(in exprContext);
							else
								input = GetUtility(ref state, ref data, in exprContext, nd.child
#if UNITY_EDITOR
								, utilityDebug
#endif
								);

							// NOTE: no saturate here so this curve can be used to normalize values
							result = nd.curve.Evaluate(input * nd.invInputRange);
							break;
						}

					case BTExec.BTExecType.UtilityRange:
						{
							ref var nd = ref node.data.utilityRange;
							var entity = nd.targetEntity.Evaluate<Entity>(in exprContext);
							if(exprContext.componentLookups[nd.lookupTypeInfo.componentIndex].TryGetRefRO(entity, out var targetComponentData))
							{
								var localComponentData = exprContext.componentPtrs[nd.localTypeInfo.componentIndex].AsNativeArray();
								var targetLtw = targetComponentData.ReinterpretLoad<Unity.Transforms.LocalToWorld>(0);
								var localLtw = localComponentData.ReinterpretLoad<Unity.Transforms.LocalToWorld>(0);
								var vector = targetLtw.Position - localLtw.Position;
								float distance = math.length(vector);
								if(nd.softRange)
									result = nd.softRangeCurve.Evaluate(distance / nd.range);
								else
									result = distance <= nd.range ? 1.0f : 0.0f;

								var input = GetUtility(ref state, ref data, in exprContext, nd.child
#if UNITY_EDITOR
									, utilityDebug
#endif
									);

								result *= input;
							}
							else
							{
								result = 0;
							}
							break;
						}

					default:
						throw new NotImplementedException();
				}

#if UNITY_EDITOR
				if(utilityDebug.IsCreated)
					utilityDebug.ElementAt(nodeId.index).value = result;
#endif

				return result;
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

		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		private static void CheckQueryNode(ref BTExec queryNode)
		{
			if(queryNode.type != BTExec.BTExecType.Query)
				throw new InvalidOperationException($"node in UtilitySelector queries list was not a Query node (found {(int)queryNode.type} instead)");
		}

		public static void DumpNodes(ref BTData data, List<string> output)
		{
			output.Add($"const data: {data.exprData.constants.Length} bytes");

			output.Add("");

			int j = 0;
			foreach(ref var exec in data.execs.AsRWSpan())
			{
				output.Add("Exec " + j.ToString() + ": " + exec.DumpString());
				j++;
			}

			output.Add("");

			j = 0;
			foreach(ref var expr in data.exprData.expressions.AsRWSpan())
			{
				output.Add("Expr " + j.ToString() + ": (TODO)");// + expr.DumpString());
			}
		}
	}
}
