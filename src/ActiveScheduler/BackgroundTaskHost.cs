// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Hooks;
using ActiveScheduler.Internal;
using ActiveScheduler.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TypeKitchen;
using TypeKitchen.Creation;
using ExecutionContext = ActiveScheduler.Models.ExecutionContext;

namespace ActiveScheduler
{
	public class BackgroundTaskHost : IDisposable
	{
		private static readonly IDictionary<Type, HandlerHooks> MethodCache =
			new ConcurrentDictionary<Type, HandlerHooks>();

		private readonly IServiceProvider _backgroundServices;
		private readonly ConcurrentDictionary<TaskScheduler, TaskFactory> _factories;
		private readonly ISafeLogger<BackgroundTaskHost> _logger;
		private readonly IOptionsMonitor<BackgroundTaskOptions> _options;
		private readonly ConcurrentDictionary<object, HandlerHooks> _pending;
		private readonly ConcurrentDictionary<int, TaskScheduler> _schedulers;
		private readonly ITypeResolver _typeResolver;

		private PushQueue<IEnumerable<BackgroundTask>> _background;
		private CancellationTokenSource _cancel;
		private PushQueue<IEnumerable<BackgroundTask>> _maintenance;
		private QueuedTaskScheduler _scheduler;

		public BackgroundTaskHost(
			IServiceProvider backgroundServices,
			IBackgroundTaskStore store,
			IBackgroundTaskSerializer serializer,
			ITypeResolver typeResolver,
			IOptionsMonitor<BackgroundTaskOptions> options,
			ISafeLogger<BackgroundTaskHost> logger)
		{
			_backgroundServices = backgroundServices;
			Store = store;
			Serializer = serializer;
			_typeResolver = typeResolver;
			_options = options;
			_logger = logger;
			options.OnChange(OnSettingsChanged);

			_schedulers = new ConcurrentDictionary<int, TaskScheduler>();
			_factories = new ConcurrentDictionary<TaskScheduler, TaskFactory>();
			_pending = new ConcurrentDictionary<object, HandlerHooks>();
			_cancel = new CancellationTokenSource();

			// dispatch thread
			_background = new PushQueue<IEnumerable<BackgroundTask>>();
			_background.Attach(WithPendingTasks);
			_background.AttachBacklog(WithOverflowTasks);
			_background.AttachUndeliverable(WithFailedTasks);

			// maintenance thread
			_maintenance = new PushQueue<IEnumerable<BackgroundTask>>();
			_maintenance.Attach(WithHangingTasks);
			_maintenance.AttachBacklog(WithHangingTasks);
			_maintenance.AttachUndeliverable(WithFailedTasks);
		}

		public IBackgroundTaskStore Store
		{
			get;
		}

		public IBackgroundTaskSerializer Serializer
		{
			get;
		}

		public BackgroundTaskOptions Options
		{
			get
			{
				var settings = _options.CurrentValue;

				var @readonly = new BackgroundTaskOptions
				{
					DelayTasks = settings.DelayTasks,
					Concurrency = settings.Concurrency,
					SleepIntervalSeconds = settings.SleepIntervalSeconds,
					IntervalFunction = settings.IntervalFunction,
					ReadAhead = settings.ReadAhead,
					MaximumAttempts = settings.MaximumAttempts,
					MaximumRuntimeSeconds = settings.MaximumRuntimeSeconds,
					DeleteOnError = settings.DeleteOnError,
					DeleteOnFailure = settings.DeleteOnFailure,
					DeleteOnSuccess = settings.DeleteOnSuccess,
					Priority = settings.Priority
				};

				return @readonly;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void OnSettingsChanged(BackgroundTaskOptions changed)
		{
			_logger.Info(() => "Background task options changed, recycling the host.");
			Stop();
			Start();
		}

		private IEnumerable<BackgroundTask> EnqueueTasks()
		{
			var tasks = Store.LockNextAvailableAsync(_options.CurrentValue.ReadAhead)
				.GetAwaiter()
				.GetResult()
				.AsList();

			return tasks;
		}

		private IEnumerable<BackgroundTask> HangingTasks()
		{
			var tasks = Store.GetHangingTasksAsync().GetAwaiter().GetResult();

			return tasks;
		}

		public void Start(bool immediate = false)
		{
			_logger.Info(() => "Starting background task host");

			if (_scheduler == null)
			{
				_scheduler = new QueuedTaskScheduler(ResolveConcurrency());
			}

			_background.Produce(EnqueueTasks, TimeSpan.FromSeconds(_options.CurrentValue.SleepIntervalSeconds));
			_background.Start(immediate);

			_maintenance.Produce(HangingTasks, TimeSpan.FromMinutes(1));
			_maintenance.Start(immediate);
		}

		private int ResolveConcurrency()
		{
			return _options.CurrentValue.Concurrency == 0
				? Environment.ProcessorCount
				: _options.CurrentValue.Concurrency;
		}

		public void Stop(CancellationToken cancellationToken = default, bool immediate = false)
		{
			_logger.Info(() => "Stopping background task host");

			var options = new ParallelOptions
			{
				CancellationToken = cancellationToken, MaxDegreeOfParallelism = ResolveConcurrency()
			};

			var pendingTasks = _pending.Where(entry => entry.Value.OnHalt != null);
			Parallel.ForEach(pendingTasks, options, async e =>
			{
				// FIXME: halt operation won't have user data in this context
				using var context = ProvisionExecutionContext(cancellationToken, null);

				// ReSharper disable once AccessToDisposedClosure (this is safe: immediately invoked and blocking)
				await e.Value.OnHalt.HaltAsync(context, immediate);
			});

			_pending.Clear();

			_scheduler?.Dispose();
			_scheduler = null;

			_background.Stop(immediate);
			_maintenance.Stop(immediate);
		}

		private ExecutionContext ProvisionExecutionContext(CancellationToken cancellationToken, string data)
		{
			var kv = new InMemoryKeyValueStore<string, object>();
			if (!string.IsNullOrWhiteSpace(data))
			{
				try
				{
					var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(data);
					foreach (var (k, v) in deserialized)
					{
						kv.AddOrUpdate(k, v);
					}
				}
				catch (Exception e)
				{
					Trace.WriteLine(e);
					throw;
				}
			}
			else
			{
				Trace.TraceInformation("no data passed to execution context");
			}

			var scope = _backgroundServices.CreateScope();
			var context = new ExecutionContext(scope, kv, cancellationToken);
			return context;
		}

		private void WithFailedTasks(IEnumerable<BackgroundTask> tasks)
		{
			// This should be impossible; we only use the pipeline to seed from a backing store, which is all or nothing
			WithPendingTasks(tasks);
		}

		private bool WithOverflowTasks(IEnumerable<BackgroundTask> tasks)
		{
			// We could have been shutting down, which is not materially different than if we had succeeded, so we should process these
			return WithPendingTasks(tasks);
		}

		private bool WithHangingTasks(IEnumerable<BackgroundTask> tasks)
		{
			return CleanUpHangingTasks(tasks);
		}

		public async Task<bool> CleanUpHangingTasksAsync()
		{
			return CleanUpHangingTasks(await Store.GetHangingTasksAsync());
		}

		private bool CleanUpHangingTasks(IEnumerable<BackgroundTask> tasks)
		{
			var now = Store.GetTaskTimestamp();

			_logger.Debug(() => "Cleaning up hanging tasks");

			foreach (var task in tasks)
			{
				if (task.LockedBy == Constants.Scheduling.EvictionUser)
				{
					if (task.DeleteOnFailure.HasValue && task.DeleteOnFailure.Value)
					{
						_logger.Debug(() => $"Deleting evicted task {task.Id}");
						Store.DeleteAsync(task);
					}

					continue;
				}

				// bump up attempts (wouldn't have reached here normally since we never unlocked)
				task.Attempts++;

				// unlock hanging task and record runtime failure
				task.LockedAt = null;
				task.LockedBy = null;
				task.LastError = ErrorStrings.ExceededRuntime;

				if (task.Attempts >= task.MaximumAttempts)
				{
					if (task.DeleteOnFailure.HasValue && task.DeleteOnFailure.Value)
					{
						_logger.Debug(() => $"Deleting task {task.Id}");
						Store.DeleteAsync(task);
					}

					task.FailedAt = now;
					task.LastError = ErrorStrings.ExceededRuntime;

					if (ShouldRepeat(task, false))
					{
						_logger.Debug(() => $"Cloning task {task.Id}");
						CloneTaskAtNextOccurrence(task).GetAwaiter().GetResult();
					}

					continue;
				}

				task.RunAt += _options.CurrentValue.IntervalFunction.NextInterval(task.Attempts);
				_logger.Debug(() => $"Rescheduling task {task.Id}");
				Store.SaveAsync(task);
			}

			return true;
		}

		private bool WithPendingTasks(IEnumerable<BackgroundTask> tasks)
		{
			var pendingTasks = new Dictionary<Task, CancellationTokenSource>();
			var subjects = new Dictionary<Task, BackgroundTask>();

			foreach (var task in tasks)
			{
				var scheduler = AcquireScheduler(task);
				var cancel = new CancellationTokenSource();
				var taskFactory = _factories[scheduler];

				var started = taskFactory.StartNew(async () => { await AttemptTaskAsync(task); }, cancel.Token);

				pendingTasks.Add(started, cancel);
				subjects.Add(started, task);
			}

			Parallel.ForEach(pendingTasks, new ParallelOptions {MaxDegreeOfParallelism = ResolveConcurrency()},
				performer =>
				{
					var task = subjects[performer.Key];
					if (Task.WaitAll(new[] {performer.Key}, task.MaximumRuntime.GetValueOrDefault()))
					{
						return;
					}

					performer.Value.Cancel();
					task.LastError = ErrorStrings.ExceededRuntime;
				});

			return true;
		}

		internal async Task<bool> AttemptTaskAsync(BackgroundTask task, bool persist = true)
		{
			if (_cancel.IsCancellationRequested)
				return false;

			var success = await DutyCycleAsync(task);
			if (persist)
				await UpdateTaskAsync(task, success);

			_cancel.Token.ThrowIfCancellationRequested();
			return success;
		}

		private async Task<bool> DutyCycleAsync(BackgroundTask task)
		{
			task.Attempts++;

			var success = await PerformAsync(task);
			if (!success)
			{
				task.RunAt = Store.GetTaskTimestamp() +
				             _options.CurrentValue.IntervalFunction.NextInterval(task.Attempts);
			}

			return success;
		}

		private async Task UpdateTaskAsync(BackgroundTask task, bool success)
		{
			var deleted = false;

			var now = Store.GetTaskTimestamp();

			if (!success)
			{
				_logger.Debug(() => "Task {Id} failed", task.Id);

				if (task.Attempts >= task.MaximumAttempts)
				{
					_logger.Debug(() => "Task {Id} exceeded its maximum attempts limit", task.Id);

					if (task.DeleteOnFailure.HasValue && task.DeleteOnFailure.Value)
					{
						_logger.Debug(() => "Deleting task {Id} on failure", task.Id);
						await Store.DeleteAsync(task);
						deleted = true;
					}

					task.FailedAt = now;
				}
			}
			else
			{
				_logger.Debug(() => "Task {Id} succeeded", task.Id);

				task.SucceededAt = now;

				if (task.DeleteOnSuccess.HasValue && task.DeleteOnSuccess.Value)
				{
					deleted = await Store.DeleteAsync(task);

					if (!deleted)
					{
						task.FailedAt = now;
						task.LastError = "Unexpected error trying to delete this successful task";
					}
				}
			}

			if (ShouldRepeat(task, success))
			{
				await CloneTaskAtNextOccurrence(task);
			}

			if (deleted)
				return;

			// unlock for other workers
			task.LockedAt = null;
			task.LockedBy = null;
			await Store.SaveAsync(task);
		}

		private async Task CloneTaskAtNextOccurrence(BackgroundTask task)
		{
			_logger.Debug(() => "Repeating recurring task {Id}", task.Id);

			var nextOccurrence = task.NextOccurrence;

			var clone = new BackgroundTask
			{
				CorrelationId = task.CorrelationId,
				Priority = task.Priority,
				Handler = task.Handler,
				DeleteOnSuccess = task.DeleteOnSuccess,
				DeleteOnFailure = task.DeleteOnFailure,
				DeleteOnError = task.DeleteOnError,
				Expression = task.Expression,
				Start = task.Start,
				End = task.End,
				ContinueOnSuccess = task.ContinueOnSuccess,
				ContinueOnFailure = task.ContinueOnFailure,
				ContinueOnError = task.ContinueOnError,
				RunAt = nextOccurrence.GetValueOrDefault(),
				MaximumAttempts = task.MaximumAttempts,
				MaximumRuntime = task.MaximumRuntime,
				Tags = task.Tags,
				Data = task.Data
			};

			await Store.SaveAsync(clone);
		}

		private static bool ShouldRepeat(BackgroundTask task, bool success)
		{
			if (!task.SucceededAt.HasValue && !task.FailedAt.HasValue || task.NextOccurrence == null)
				return false;

			return success && task.ContinueOnSuccess ||
			       !success && task.ContinueOnFailure ||
			       task.LastError != null && task.ContinueOnError;
		}

		private async Task<bool> PerformAsync(BackgroundTask task)
		{
			var handler = CreateHandler(task);

			if (handler == null)
			{
				task.LastError = ErrorStrings.InvalidHandler;
				return false;
			}

			var hooks = GetOrCreateMethodHooks(handler);

			using var context = ProvisionExecutionContext(_cancel.Token, task.Data);

			_pending.TryAdd(handler, hooks);

			try
			{
				// Before:
				if (hooks.OnBefore != null)
				{
					await hooks.OnBefore.BeforeAsync(context);
				}

				// Handler:
				if (context.Continue)
				{
					await hooks.Handler.PerformAsync(context);
				}

				// Success:
				if (context.Successful && hooks.OnSuccess != null)
				{
					await hooks.OnSuccess.SuccessAsync(context);
				}

				// Failure:
				if (!context.Successful || task.Attempts >= task.MaximumAttempts)
				{
					if (hooks.OnFailure != null)
					{
						await hooks.OnFailure?.FailureAsync(context);
					}
				}

				// After:
				if (hooks.OnAfter != null)
				{
					await hooks.OnAfter?.AfterAsync(context);
				}
			}
			catch (OperationCanceledException)
			{
				task.LastError = "Cancelled";
			}
			catch (Exception e)
			{
				task.LastError = e.Message;
				hooks?.OnError?.ErrorAsync(context, e);
			}
			finally
			{
				// Error (passed via context):
				if (task.LastError != null && hooks?.OnError != null && context.Error != null)
				{
					task.LastError = context.Error.Message;
					hooks.OnError?.ErrorAsync(context, context.Error);
				}

				_pending.TryRemove(handler, out _);
			}

			return context.Successful;
		}

		private object CreateHandler(BackgroundTask task)
		{
			try
			{
				var handlerInfo = Serializer.Deserialize<HandlerInfo>(task.Handler);

				var typeName = $"{handlerInfo.Namespace}.{handlerInfo.Entrypoint}";
				var type = _typeResolver.FindByFullName(typeName) ?? _typeResolver.FindFirstByName(typeName);
				return type == null ? null : _backgroundServices.AutoResolve(type);
			}
			catch (Exception e)
			{
				_logger.Error(() => "Error creating handler for task", e);
				throw;
			}
		}

		// ReSharper disable once InconsistentNaming
		private static HandlerHooks GetOrCreateMethodHooks(object handler)
		{
			var handlerType = handler.GetType();
			if (!MethodCache.TryGetValue(handlerType, out var methods))
			{
				MethodCache.Add(handlerType,
					methods = new HandlerHooks
					{
						Handler = TryWrapHook<Handler>(handler),
						OnBefore = TryWrapHook<Before>(handler),
						OnAfter = TryWrapHook<After>(handler),
						OnSuccess = TryWrapHook<Success>(handler),
						OnFailure = TryWrapHook<Failure>(handler),
						OnError = TryWrapHook<Error>(handler),
						OnHalt = TryWrapHook<Halt>(handler)
					});
			}

			return methods;
		}

		private static T TryWrapHook<T>(object instance) where T : class
		{
			var prototype = typeof(T).GetMethods();
			var example = instance.GetType().GetMethods();
			var match = prototype.Any(l => example.Any(r => AreMethodsDuckEquivalent(l, r)))
				? instance.QuackLike<T>()
				: null;
			return match;
		}

		private static bool AreMethodsDuckEquivalent(MethodInfo left, MethodInfo right)
		{
			if (left == null || right == null)
			{
				return false;
			}

			if (!left.Name.Equals(right.Name))
			{
				return false;
			}

			if (left.Equals(right) || left.GetHashCode() == right.GetHashCode())
			{
				return true;
			}

			var lp = left.GetParameters();
			var rp = right.GetParameters();
			if (lp.Length != rp.Length)
			{
				return false;
			}

			if (lp.Where((t, i) => t.ParameterType != rp[i].ParameterType).Any())
			{
				return false;
			}

			return left.ReturnType == right.ReturnType;
		}

		private TaskScheduler AcquireScheduler(BackgroundTask task)
		{
			if (_schedulers.TryGetValue(task.Priority, out var scheduler))
			{
				return scheduler;
			}

			scheduler = _scheduler.ActivateNewQueue(task.Priority);
			var factory = new TaskFactory(_cancel.Token, TaskCreationOptions.LongRunning,
				TaskContinuationOptions.LongRunning, scheduler);
			_schedulers.TryAdd(task.Priority, scheduler);
			_factories.TryAdd(scheduler, factory);

			return scheduler;
		}

		protected void Dispose(bool disposing)
		{
			if (!disposing)
			{
				return;
			}

			if (_cancel != null)
			{
				_cancel.Cancel();
				_cancel.Token.WaitHandle.WaitOne();
				_cancel.Dispose();
				_cancel = null;
			}

			_factories?.Clear();
			_schedulers?.Clear();

			_scheduler?.Dispose();
			_scheduler = null;

			_background?.Dispose();
			_background = null;

			_maintenance?.Dispose();
			_maintenance = null;
		}
	}
}