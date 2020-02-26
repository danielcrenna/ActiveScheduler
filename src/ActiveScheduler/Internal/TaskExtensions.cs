// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ActiveScheduler.Internal
{
	internal static class TaskExtensions
	{
		private static readonly IDictionary<TaskScheduler, TaskFactory> TaskFactories =
			new ConcurrentDictionary<TaskScheduler, TaskFactory>();

		public static Task Run(this TaskScheduler scheduler, Action action, CancellationToken cancellationToken)
		{
			return WithTaskFactory(scheduler).StartNew(action, cancellationToken);
		}
		
		public static Task Run(this TaskScheduler scheduler, Func<Task> func, CancellationToken cancellationToken)
		{
			return WithTaskFactory(scheduler).StartNew(func, cancellationToken).Unwrap();
		}
		
		public static TaskFactory WithTaskFactory(this TaskScheduler scheduler)
		{
			if (!TaskFactories.TryGetValue(scheduler, out var tf))
			{
				TaskFactories.Add(scheduler,
					tf = new TaskFactory(CancellationToken.None, TaskCreationOptions.DenyChildAttach,
						TaskContinuationOptions.None, scheduler));
			}

			return tf;
		}
	}
}