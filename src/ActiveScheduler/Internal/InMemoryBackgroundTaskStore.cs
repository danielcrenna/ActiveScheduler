// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActiveScheduler.Models;
using TypeKitchen;

namespace ActiveScheduler.Internal
{
	internal class InMemoryBackgroundTaskStore : IBackgroundTaskStore
	{
		private static int _identity;

		private readonly IDictionary<int, HashSet<BackgroundTask>> _tasks;
		private readonly IServerTimestampService _timestamps;

		public InMemoryBackgroundTaskStore(IServerTimestampService timestamps)
		{
			_timestamps = timestamps;
			_tasks = new ConcurrentDictionary<int, HashSet<BackgroundTask>>();
		}

		public async Task<IEnumerable<BackgroundTask>> GetByAnyTagsAsync(params string[] tags)
		{
			var all = await GetAllAsync();

			var query = all.Where(a => { return tags.Any(tag => a.Tags.Contains(tag)); });

			return query.ToList();
		}

		public async Task<IEnumerable<BackgroundTask>> GetByAllTagsAsync(params string[] tags)
		{
			var all = await GetAllAsync();

			var query = all.Where(a => { return tags.All(tag => a.Tags.Contains(tag)); });

			return query.ToList();
		}

		public Task<bool> SaveAsync(BackgroundTask task)
		{
			if (!_tasks.TryGetValue(task.Priority, out var tasks))
			{
				task.CreatedAt = GetTaskTimestamp();
				_tasks.Add(task.Priority, tasks = new HashSet<BackgroundTask>());
			}

			if (tasks.All(t => t.Id != task.Id))
			{
				tasks.Add(task);
				task.Id = ++_identity;
				return Task.FromResult(true);
			}

			return Task.FromResult(false);
		}

		public Task<bool> DeleteAsync(BackgroundTask task)
		{
			if (_tasks.TryGetValue(task.Priority, out var tasks))
			{
				tasks.Remove(task);
				return Task.FromResult(true);
			}

			return Task.FromResult(false);
		}

		public Task<IEnumerable<BackgroundTask>> LockNextAvailableAsync(int readAhead)
		{
			var all = _tasks.SelectMany(t => t.Value);

			// None locked, failed or succeeded, must be due, ordered by due time then priority
			var now = GetTaskTimestamp();

			var query = all
				.Where(t => !t.FailedAt.HasValue && !t.SucceededAt.HasValue && !t.LockedAt.HasValue)
				.Where(t => t.RunAt <= now)
				.OrderBy(t => t.RunAt)
				.ThenBy(t => t.Priority)
				.AsList();

			var tasks = (query.Count > readAhead ? query.Take(readAhead) : query).AsList();

			// Lock tasks:
			if (tasks.Any())
			{
				foreach (var scheduledTask in tasks)
				{
					scheduledTask.LockedAt = now;
					scheduledTask.LockedBy = LockedIdentity.Get();
				}
			}

			return Task.FromResult(tasks.AsEnumerable());
		}

		public DateTimeOffset GetTaskTimestamp()
		{
			return _timestamps.GetCurrentTime().ToUniversalTime();
		}

		public Task<BackgroundTask> GetByIdAsync(int id)
		{
			return Task.FromResult(_tasks.SelectMany(t => t.Value).SingleOrDefault(t => t.Id == id));
		}

		public async Task<IEnumerable<BackgroundTask>> GetHangingTasksAsync()
		{
			var tasks = await GetAllAsync();

			return tasks.Where(t => t.IsRunningOvertime(this)).ToList();
		}

		public Task<IEnumerable<BackgroundTask>> GetAllAsync()
		{
			IEnumerable<BackgroundTask> all = _tasks.SelectMany(t => t.Value).OrderBy(t => t.Priority);

			return Task.FromResult(all);
		}

		public void Clear()
		{
			_tasks.Clear();
		}
	}
}