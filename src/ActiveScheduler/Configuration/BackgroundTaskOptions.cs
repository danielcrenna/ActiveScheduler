// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveRoutes;
using Constants = ActiveScheduler.Internal.Constants;

namespace ActiveScheduler.Configuration
{
	public class BackgroundTaskOptions : IFeatureToggle, IFeatureScheme, IFeaturePolicy, IFeatureNamespace
	{
		public BackgroundTaskOptions()
		{
			// System:
			DelayTasks = true;
			SleepIntervalSeconds = 10;
			CleanupIntervalSeconds = 300;
			Concurrency = 0;
			ReadAhead = 5;
			MaximumAttempts = 25;
			MaximumRuntimeSeconds = 180;
			IntervalFunction = IntervalFunction.ExponentialBackoff;
			Store = new StoreOptions();

			// Per-Task:
			DeleteOnFailure = false;
			DeleteOnSuccess = true;
			DeleteOnError = false;
			Priority = 0;
		}

		/// <summary>
		///     The function responsible for calculating the next attempt date after a tasks fails;
		///     default is 5 seconds + N.Pow(4), where N is the number of retries (i.e. exponential back-off)
		/// </summary>
		public IntervalFunction IntervalFunction { get; set; }

		/// <summary>
		///     Whether or not tasks are delayed or executed immediately; default is true
		/// </summary>
		public bool DelayTasks { get; set; }

		/// <summary>
		///     The time to delay before checking for available tasks in the backing store. Default is 10 seconds.
		/// </summary>
		public int SleepIntervalSeconds { get; set; }

		/// <summary>
		///     The time to delay before checking for hanging tasks in the backing store. Default is 3 minutes.
		/// </summary>
		public int CleanupIntervalSeconds { get; set; }

		/// <summary>
		///     The number of threads available for performing tasks; default is 0.
		///     A value of 0 defaults to the number of logical processors.
		/// </summary>
		public int Concurrency { get; set; }

		/// <summary>
		///     The number of jobs to pull at once when searching for available tasks; default is 5.
		/// </summary>
		public int ReadAhead { get; set; }

		/// <summary>
		///     The maximum number of attempts made before failing a task permanently; default is 25.
		/// </summary>
		public int MaximumAttempts { get; set; }

		/// <summary>
		///     The maximum time each task is allowed before being cancelled; default is 3 minutes.
		/// </summary>
		public int MaximumRuntimeSeconds { get; set; }

		/// <summary>
		///     Whether or not failed tasks are deleted from the backend store; default is false.
		/// </summary>
		public bool DeleteOnFailure { get; set; }

		/// <summary>
		///     Whether or not successful tasks are deleted from the backend store; default is true.
		/// </summary>
		public bool DeleteOnSuccess { get; set; }

		/// <summary>
		///     Whether or not tasks that throw exceptions are deleted from the backend store; default is false.
		/// </summary>
		public bool DeleteOnError { get; set; }

		/// <summary>
		///     The default priority level for newly created scheduled tasks that don't specify a priority; default is 0, or
		///     highest priority
		/// </summary>
		public int Priority { get; set; }

		public StoreOptions Store { get; set; }
		public string RootPath { get; set; } = "/ops";
		public string Policy { get; set; } = Constants.Security.Policies.ManageBackgroundTasks;
		public string Scheme { get; set; }
		public bool Enabled { get; set; } = true;

		/// <summary> Set task values that have defaults if not provided by the user. </summary>
		public void ProvisionTask(BackgroundTask task)
		{
			task.MaximumRuntime ??= TimeSpan.FromSeconds(MaximumRuntimeSeconds);
			task.MaximumAttempts ??= MaximumAttempts;
			task.DeleteOnSuccess ??= DeleteOnSuccess;
			task.DeleteOnFailure ??= DeleteOnFailure;
			task.DeleteOnError ??= DeleteOnError;
		}
	}
}