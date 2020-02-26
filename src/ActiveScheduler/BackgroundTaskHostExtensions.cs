// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;

namespace ActiveScheduler
{
	public static class BackgroundTaskHostExtensions
	{
		/// <summary>
		///     Schedules a new task for delayed execution for the given host.
		///     If the user does NOT provide a RunAt during options, but an expression IS provided, the next occurrence of the
		///     expression, relative to now, will be selected as the start time.
		///     Otherwise, the task will be scheduled for now.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="type"></param>
		/// <param name="userData"></param>
		/// <param name="options">
		///     Allows configuring task-specific features. Note that this is NOT invoked at invocation time
		///     lazily, but at scheduling time (i.e. immediately).
		/// </param>
		/// <returns>
		///     Whether the scheduled operation was successful; if `true`, it was either scheduled or ran successfully,
		///     depending on configuration. If `false`, it either failed to schedule or failed during execution, depending on
		///     configuration.
		/// </returns>
		public static Task<(bool, BackgroundTask)> TryScheduleTaskAsync(this BackgroundTaskHost host, Type type,
			object userData = null, Action<BackgroundTask> options = null)
		{
			return host.QueueForExecutionAsync(type, userData, options);
		}

		private static async Task<(bool, BackgroundTask)> QueueForExecutionAsync(this BackgroundTaskHost host,
			Type type, object userData, Action<BackgroundTask> options)
		{
			var task = NewTask(host.Options, host.Serializer, type, userData);

			options?.Invoke(task); // <-- at this stage, task should have a RunAt set by the user or it will be default

			if (!string.IsNullOrWhiteSpace(task.Expression) && !task.HasValidExpression)
			{
				throw new ArgumentException("The provided CRON expression is invalid.");
			}

			// Handle when no start time is provided up front
			if (task.RunAt == default)
			{
				task.RunAt = host.Store.GetTaskTimestamp();

				if (task.NextOccurrence.HasValue)
				{
					task.RunAt = task.NextOccurrence.Value;
				}
			}

			// Set the "Start" property only once, equal to the very first RunAt 
			task.Start = task.RunAt;

			if (host.Store != null)
			{
				await host.Store.SaveAsync(task);
			}

			if (!host.Options.DelayTasks)
			{
				return (await host.AttemptTaskAsync(task, false), task);
			}

			return (true, task);
		}

		private static BackgroundTask NewTask(BackgroundTaskOptions options, IBackgroundTaskSerializer serializer,
			Type type, object userData)
		{
			var handlerInfo = new HandlerInfo(type.Namespace, type.Name);
			var task = new BackgroundTask
			{
				Handler = serializer?.Serialize(handlerInfo),
				Data = userData == null ? null : JsonSerializer.Serialize(userData)
			};
			options.ProvisionTask(task);
			return task;
		}
	}
}