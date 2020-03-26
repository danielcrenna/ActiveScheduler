// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;
using ActiveScheduler.Tests.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TypeKitchen;
using Xunit;
using Xunit.Abstractions;

namespace ActiveScheduler.Tests
{
	public abstract class BackgroundTaskHostTests : ServiceUnderTest
	{
		protected BackgroundTaskHostTests(IServiceProvider serviceProvider, ITestOutputHelper console) : base(
			serviceProvider)
		{
			_console = console;
			Store = ServiceProvider.GetRequiredService(typeof(IBackgroundTaskStore)) as IBackgroundTaskStore;
		}

		private readonly ITestOutputHelper _console;

		protected readonly IBackgroundTaskStore Store;

		private BackgroundTaskHost CreateBackgroundTaskHost(Action<BackgroundTaskOptions> configureOptions)
		{
			var services = new ServiceCollection();
			services.AddBackgroundTasks(configureOptions);

			var serviceProvider = services.BuildServiceProvider();

			var serializer = new JsonBackgroundTaskSerializer();
			var typeResolver = new ReflectionTypeResolver();
			var options = serviceProvider.GetRequiredService<IOptionsMonitor<BackgroundTaskOptions>>();
			var host = serviceProvider.GetService<ISafeLogger<BackgroundTaskHost>>();

			var scheduler = new BackgroundTaskHost(serviceProvider, Store, serializer, typeResolver, options, host);
			return scheduler;
		}

		[Fact]
		public async Task Can_cleanup_hanging_tasks()
		{
			using var host = CreateBackgroundTaskHost(o =>
			{
				o.DelayTasks = true;
				o.MaximumAttempts = 1;
				o.MaximumRuntimeSeconds = 1;
				o.SleepIntervalSeconds = 1;

				o.CleanupIntervalSeconds = 1000;
			});

			host.Start();
			{
				await host.TryScheduleTaskAsync(typeof(TerminalTaskHandler));

				var all = (await Store.GetAllAsync()).ToList();
				Assert.Equal(1, all.Count /*, "Queue task should exist"*/);

				await Task.Delay(TimeSpan.FromSeconds(3)); // <-- enough time to have started the terminal task

				all = (await Store.GetAllAsync()).ToList();
				Assert.Equal(1, all.Count /*, "Queue task should still exist, since it is terminal"*/);

				var task = all.First();
				Assert.True(task.LockedAt.HasValue, "Queue task should be locked");
				Assert.True(task.MaximumRuntime.HasValue, "Queue task should have a maximum runtime set.");
				Assert.True(task.IsRunningOvertime(Store), "Queue task should be running overtime");

				var hanging = (await Store.GetHangingTasksAsync()).ToList();
				Assert.Equal(1, hanging.Count /*, "Hanging task is not considered hanging by the task store"*/);

				var result = await host.CleanUpHangingTasksAsync();
				Assert.True(result, "Hanging task operation did not return successfully.");

				var threadId = 0;

				await Task.Run(async () =>
				{
					var original = Interlocked.Exchange(ref threadId, Thread.CurrentThread.ManagedThreadId);
					Assert.Equal(0, original);
					return result = await host.CleanUpHangingTasksAsync();
				});

				await Task.Run(async () =>
				{
					var original = Interlocked.Exchange(ref threadId, Thread.CurrentThread.ManagedThreadId);
					Assert.Equal(threadId,
						original /*, "Unexpected DI resolution of the second async cleanup thread"*/);
					return result = await host.CleanUpHangingTasksAsync();
				});
			}

			host.Stop();
		}

		[Fact]
		public async Task Queues_for_delayed_execution()
		{
			var host = CreateBackgroundTaskHost(o =>
			{
				o.DelayTasks = true;
				o.Concurrency = 1;
				o.SleepIntervalSeconds = 1;
			});
			await host.TryScheduleTaskAsync(typeof(StaticCountingHandler), null, o =>
			{
				o.RunAt =
					Store.GetTaskTimestamp() + TimeSpan.FromSeconds(1);
			});

			Assert.True(StaticCountingHandler.Count == 0,
				"handler should not have queued immediately since tasks are delayed");

			host.Start(); // <-- starts background thread to poll for tasks

			await Task.Delay(2000); // <-- enough time for the next occurrence
			Assert.True(StaticCountingHandler.Count > 0,
				"handler should have executed since we scheduled it in the future");
			Assert.True(StaticCountingHandler.Count == 1,
				"handler should have only executed once since it does not repeat");
		}

		[Fact]
		public void Queues_for_delayed_execution_and_continuous_repeating_task()
		{
			var host = CreateBackgroundTaskHost(o =>
			{
				o.DelayTasks = true;
				o.Concurrency = 1;
				o.SleepIntervalSeconds = 1;
			});
			host.TryScheduleTaskAsync(typeof(StaticCountingHandler), null, o =>
			{
				o.Data = "{ \"Foo\": \"123\" }";
				o.RunAt = Store.GetTaskTimestamp() + TimeSpan.FromSeconds(1);
				o.RepeatIndefinitely(CronTemplates.Secondly(1));
			});
			host.Start(); // <-- starts background thread to poll for tasks

			Assert.True(StaticCountingHandler.Count == 0,
				"handler should not have queued immediately since tasks are delayed");

			Thread.Sleep(TimeSpan.FromSeconds(2)); // <-- enough time for the next occurrence
			Assert.True(StaticCountingHandler.Count > 0,
				"handler should have executed since we scheduled it in the future");
			Assert.NotNull(StaticCountingHandler.Data /*, "handler did not preserve data"*/);
			Assert.Equal((string) StaticCountingHandler.Data, "123" /*, "handler misread data"*/);

			StaticCountingHandler.Data = null;

			Thread.Sleep(TimeSpan.FromSeconds(2)); // <-- enough time for the next occurrence
			Assert.True(StaticCountingHandler.Count >= 2);
			Assert.NotNull(StaticCountingHandler.Data /*, "handler did not preserve data"*/);
			Assert.Equal(StaticCountingHandler.Data, "123" /*, "handler misread data"*/);
		}

		[Fact]
		public void Queues_for_immediate_execution()
		{
			var host = CreateBackgroundTaskHost(o => { o.DelayTasks = false; });
			host.TryScheduleTaskAsync(typeof(StaticCountingHandler));

			Thread.Sleep(TimeSpan.FromMilliseconds(100)); // <-- enough time for the occurrence

			Assert.True(StaticCountingHandler.Count == 1,
				"handler should have queued immediately since tasks are not delayed");
		}

		[Fact]
		public void Starts_and_stops()
		{
			using var host = CreateBackgroundTaskHost(o => { });
			host.Start();
			host.Stop();
		}
	}
}