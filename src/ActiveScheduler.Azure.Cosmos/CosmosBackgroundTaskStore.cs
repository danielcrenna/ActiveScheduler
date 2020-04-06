// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ActiveLogging;
using ActiveScheduler.Models;
using ActiveStorage.Azure.Cosmos;

namespace ActiveScheduler.Azure.Cosmos
{
	public class CosmosBackgroundTaskStore : IBackgroundTaskStore
	{
		private readonly ISafeLogger<CosmosBackgroundTaskStore> _logger;
		private readonly ICosmosRepository _repository;
		private readonly Func<DateTimeOffset> _timestamps;

		public CosmosBackgroundTaskStore(ICosmosRepository repository,
			Func<DateTimeOffset> timestamps, ISafeLogger<CosmosBackgroundTaskStore> logger)
		{
			_repository = repository;
			_timestamps = timestamps;
			_logger = logger;
		}

		public async Task<BackgroundTask> GetByIdAsync(int id)
		{
			var task = await _repository.RetrieveSingleOrDefaultAsync(x => x.TaskId == id);

			// ReSharper disable once UseNullPropagation (implicit conversion will fail)
			// ReSharper disable once ConvertIfStatementToReturnStatement (implicit conversion will fail)
			if (task == null)
				return null;

			return task;
		}

		public async Task<IEnumerable<BackgroundTask>> GetAllAsync()
		{
			var tasks = await _repository.RetrieveAsync<BackgroundTask>();

			return tasks.Select(x => (BackgroundTask) x);
		}

		public async Task<IEnumerable<BackgroundTask>> GetByAllTagsAsync(params string[] tags)
		{
			// DB doesn't support All expression, so we have to project Any then filter it client-side
			// ReSharper disable once ConvertClosureToMethodGroup (DB doesn't recognize method groups)
			var tasks = await _repository.RetrieveAsync(x => x.Tags.Any(t => tags.Contains(t)));

			// Reduce "any" to "all" on the client
			var all = tasks.Where(x => tags.All(t => x.Tags.Contains(t)));

			return all.Select(x => (BackgroundTask) x);
		}

		public async Task<IEnumerable<BackgroundTask>> GetByAnyTagsAsync(params string[] tags)
		{
			// ReSharper disable once ConvertClosureToMethodGroup (DB doesn't recognize method groups)
			var tasks = await _repository.RetrieveAsync(x => x.Tags.Any(t => tags.Contains(t)));

			return tasks.Select(x => (BackgroundTask) x);
		}

		public async Task<IEnumerable<BackgroundTask>> GetHangingTasksAsync()
		{
			var tasks = await _repository.RetrieveAsync(x => x.LockedAt.HasValue);

			return tasks.Select(x => (BackgroundTask) x).Where(x => x.IsRunningOvertime(this));
		}

		public async Task<bool> SaveAsync(BackgroundTask task)
		{
			var document = new BackgroundTaskDocument(task);

			if (task.Id == 0)
			{
				await _repository.CreateAsync(document);
				task.Id = document.TaskId;
				return true;
			}

			var existing = await _repository.RetrieveSingleOrDefaultAsync(x => x.TaskId == task.Id);
			if (existing == null)
			{
				await _repository.CreateAsync(document);
				_logger.Trace(() => "Creating new task with ID {Id} and handler '{Handler}'", document.Id,
					document.Handler);
				return true;
			}

			document.Id = existing.Id;
			_logger.Trace(() => "Updating existing task with ID {Id} and handler '{Handler}'", document.Id,
				document.Handler);
			await _repository.UpdateAsync(existing.Id, document);
			return true;
		}

		public async Task<bool> DeleteAsync(BackgroundTask task)
		{
			var document = await _repository.RetrieveSingleOrDefaultAsync(x => x.TaskId == task.Id);
			if (document == null)
			{
				_logger.Warn(() => "Could not delete task with ID {Id} as it was not found.", task.Id);
				return false;
			}

			var deleted = await _repository.DeleteAsync(document.Id);
			if (!deleted)
			{
				_logger.Warn(() => "Could not delete task with ID {Id} successfully.", task.Id);
				return false;
			}

			return true;
		}

		public async Task<IEnumerable<BackgroundTask>> LockNextAvailableAsync(int readAhead)
		{
			var now = GetTaskTimestamp();

			var tasks = (await _repository.RetrieveAsync(x =>
				x.LockedAt == null &&
				x.FailedAt == null &&
				x.SucceededAt == null &&
				x.RunAt <= now)).ToList();

			foreach (var task in tasks)
			{
				task.LockedAt = now;
				task.LockedBy = LockedIdentity.Get();
				await _repository.UpdateAsync(task.Id, task);
			}

			return tasks
				.OrderBy(x => x.RunAt)
				.ThenBy(x => x.Priority)
				.Select(x => (BackgroundTask) x);
		}

		public DateTimeOffset GetTaskTimestamp()
		{
			return _timestamps().ToUniversalTime();
		}
	}
}