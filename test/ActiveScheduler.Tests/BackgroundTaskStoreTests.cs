// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;
using ActiveScheduler.Tests.Internal;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActiveScheduler.Tests
{
	public abstract class BackgroundTaskStoreTests : ServiceUnderTest
	{
		protected BackgroundTaskStoreTests(IServiceProvider serviceProvider) : base(serviceProvider) => Store =
			ServiceProvider.GetRequiredService(typeof(IBackgroundTaskStore)) as IBackgroundTaskStore;

		protected readonly IBackgroundTaskStore Store;

		private BackgroundTask CreateNewTask()
		{
			var task = new BackgroundTask();
			var options = new BackgroundTaskOptions();

			// these values are required and must be set by implementation
			task.Handler = "{}";
			task.MaximumAttempts = options.MaximumAttempts;
			task.MaximumRuntime = TimeSpan.FromSeconds(options.MaximumRuntimeSeconds);
			task.DeleteOnError = options.DeleteOnError;
			task.DeleteOnFailure = options.DeleteOnFailure;
			task.DeleteOnSuccess = options.DeleteOnSuccess;
			task.RunAt = Store.GetTaskTimestamp();

			return task;
		}

		[Fact]
		public async Task Adding_tags_synchronizes_with_store()
		{
			var create = CreateNewTask();
			create.Tags.Add("a");

			var created = await Store.SaveAsync(create);
			Assert.True(created, "Task not created");

			var exists = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(exists /*, $"Created task with ID '{create.Id}' is not visible"*/);

			exists.Tags.Add("b");
			await Store.SaveAsync(exists);

			exists.Tags.Add("c");
			await Store.SaveAsync(exists);

			var all = (await Store.GetAllAsync()).AsList();
			Assert.Equal(1, all.Count);
			Assert.Equal(3, all.Single().Tags.Count);
		}

		[Fact]
		public async Task Can_delete_tasks_with_tags()
		{
			var create = CreateNewTask();
			create.Tags.Add("a");
			create.Tags.Add("b");
			create.Tags.Add("c");

			await Store.SaveAsync(create);

			var created = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(created /*, $"Created task with ID '{create.Id}' is not visible"*/);

			await Store.DeleteAsync(create);

			var deleted = await Store.GetByIdAsync(create.Id);
			Assert.Null(deleted);
		}

		[Fact]
		public async Task Can_save_multiple_tasks_with_tags()
		{
			var first = CreateNewTask();
			first.Tags.Add("one");
			await Store.SaveAsync(first);

			var second = CreateNewTask();
			second.Tags.Add("two");
			await Store.SaveAsync(second);

			var tasks = (await Store.GetByAllTagsAsync("one")).AsList();
			Assert.True(tasks.Count == 1);
			Assert.True(tasks.Single().Tags.Count == 1);

			await Store.DeleteAsync(second);
		}

		[Fact]
		public async Task Can_search_for_all_tags()
		{
			var create = CreateNewTask();
			create.Tags.Add("a");
			create.Tags.Add("b");
			create.Tags.Add("c");
			await Store.SaveAsync(create);

			var created = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(created /*, $"Created task with ID '{create.Id}' is not visible"*/);

			// GetByAllTagsAsync (miss):
			var all = (await Store.GetByAllTagsAsync("a", "b", "c", "d")).AsList();
			Assert.Equal(0, all.Count /*, "Result returned that doesn't contain all tags"*/);

			// GetByAnyTagsAsync (hit):
			all = (await Store.GetByAllTagsAsync("a", "b", "c")).AsList();
			Assert.Equal(1, all.Count);
			Assert.Equal(3, all.Single().Tags.Count);
		}

		[Fact]
		public async Task Can_search_for_any_tags()
		{
			var create = CreateNewTask();
			create.Tags.Add("a");
			create.Tags.Add("b");
			create.Tags.Add("c");
			await Store.SaveAsync(create);

			var created = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(created /*, $"Created task with ID '{create.Id}' is not visible"*/);

			// GetByAllTagsAsync (miss):
			var all = (await Store.GetByAnyTagsAsync("e")).AsList();
			Assert.Equal(0, all.Count);

			// GetByAnyTagsAsync (hit):
			all = (await Store.GetByAnyTagsAsync("e", "a")).AsList();
			Assert.Equal(1, all.Count);
			Assert.Equal(3, all.Single().Tags.Count);
		}

		[Fact]
		public async Task Correlated_tasks_are_evicted_on_lock()
		{
			var correlationId = Guid.NewGuid();

			var first = CreateNewTask();
			first.CorrelationId = correlationId;
			await Store.SaveAsync(first);

			var second = CreateNewTask();
			second.CorrelationId = correlationId;
			await Store.SaveAsync(second);

			var locked = (await Store.LockNextAvailableAsync(int.MaxValue)).AsList();
			Assert.False(!locked.Any(), "did not retrieve at least one unlocked task");

			locked = (await Store.LockNextAvailableAsync(int.MaxValue)).AsList();
			Assert.True(!locked.Any(), "there was at least one unlocked task after locking all of them");
		}

		[Fact]
		public async Task Inserts_new_task()
		{
			var create = CreateNewTask();

			Assert.True(create.Id == 0);
			await Store.SaveAsync(create);
			Assert.False(create.Id == 0);

			var created = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(created /*, $"Created task with ID '{create.Id}' is not visible"*/);
			Assert.Equal(create.Id, created.Id);
		}

		[Fact]
		public async Task Locked_tasks_are_not_visible_to_future_fetches()
		{
			var created = CreateNewTask();

			await Store.SaveAsync(created);

			var locked = (await Store.LockNextAvailableAsync(int.MaxValue)).AsList();
			Assert.False(!locked.Any(), "did not retrieve at least one unlocked task");

			locked = (await Store.LockNextAvailableAsync(int.MaxValue)).AsList();
			Assert.True(!locked.Any(), "there was at least one unlocked task after locking all of them");
		}

		[Fact]
		public async Task Removing_tags_synchronizes_with_store()
		{
			var create = CreateNewTask();
			create.Tags.Add("a");
			create.Tags.Add("b");
			create.Tags.Add("c");
			await Store.SaveAsync(create);

			var created = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(created /*, $"Created task with ID '{create.Id}' is not visible"*/);

			create.Tags.Remove("a");
			await Store.SaveAsync(create);

			var all = (await Store.GetAllAsync()).AsList();
			Assert.Equal(1, all.Count);
			Assert.Equal(2, all.First().Tags.Count);

			var byId = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(byId);
			Assert.Equal(2, byId.Tags.Count);

			create.Tags.Clear();
			await Store.SaveAsync(create);

			byId = await Store.GetByIdAsync(create.Id);
			Assert.NotNull(byId);
			Assert.True(byId.Tags.Count == 0);
		}

		[Fact]
		public async Task Tags_are_saved_with_tasks()
		{
			var created = CreateNewTask();
			created.Tags.Add("a");
			created.Tags.Add("b");
			created.Tags.Add("c");
			await Store.SaveAsync(created);

			var all = (await Store.GetAllAsync()).AsList();
			Assert.Equal(1, all.Count);
			Assert.Equal(3, all.Single().Tags.Count);

			var byId = await Store.GetByIdAsync(created.Id);
			Assert.NotNull(byId);
			Assert.Equal(3, byId.Tags.Count);

			var locked = (await Store.LockNextAvailableAsync(1)).AsList();
			Assert.Equal(1, locked.Count);
			Assert.Equal(3, locked.Single().Tags.Count);
		}
	}
}