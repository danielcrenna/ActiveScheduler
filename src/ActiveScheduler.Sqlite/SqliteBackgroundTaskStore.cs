// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ActiveConnection;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;
using ActiveScheduler.Sqlite.Internal;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.Sqlite
{
	public sealed class SqliteBackgroundTaskStore : IBackgroundTaskStore
	{
		private static readonly List<string> NoTags = new List<string>();
		private readonly ISafeLogger<SqliteBackgroundTaskStore> _logger;
		private readonly IOptionsMonitor<BackgroundTaskOptions> _options;

		private readonly IServiceProvider _serviceProvider;
		private readonly string _tablePrefix;
		private readonly Func<DateTimeOffset> _timestamps;

		private WeakReference<IDataConnection> _lastDataConnection;

		private WeakReference<IDbConnection> _lastDbConnection;

		public SqliteBackgroundTaskStore(IServiceProvider serviceProvider,
			Func<DateTimeOffset> timestamps,
			IOptionsMonitor<BackgroundTaskOptions> options,
			string tablePrefix = nameof(BackgroundTask),
			ISafeLogger<SqliteBackgroundTaskStore> logger = null)
		{
			_serviceProvider = serviceProvider;
			_timestamps = timestamps;
			_options = options;
			_tablePrefix = tablePrefix;
			_logger = logger;
		}

		internal string TaskTable => $"\"{_tablePrefix}\"";
		internal string TagTable => $"\"{_tablePrefix}_Tag\"";
		internal string TagsTable => $"\"{_tablePrefix}_Tags\"";

		public async Task<IEnumerable<BackgroundTask>> GetByAnyTagsAsync(params string[] tags)
		{
			var db = GetDbConnection();
			using var t = BeginTransaction(db, out _);
			return await GetByAnyTagsWithTagsAsync(tags, db, t);
		}

		public async Task<IEnumerable<BackgroundTask>> GetByAllTagsAsync(params string[] tags)
		{
			var db = GetDbConnection();
			using var t = BeginTransaction(db, out _);
			return await GetByAllTagsWithTagsAsync(tags, db, t);
		}

		public async Task<IEnumerable<BackgroundTask>> LockNextAvailableAsync(int readAhead)
		{
			var db = GetDbConnection();

			List<BackgroundTask> tasks;
			using (var t = BeginTransaction(db, out var owner))
			{
				tasks = await GetUnlockedTasksWithTagsAsync(readAhead, db, t);
				if (tasks.Any())
					await LockTasksAsync(tasks, db, t);

				if (owner)
					t.Commit();
			}

			return tasks;
		}

		public async Task<BackgroundTask> GetByIdAsync(int id)
		{
			var db = GetDbConnection();
			BackgroundTask task;
			using (var t = BeginTransaction(db, out _))
				task = await GetByIdWithTagsAsync(id, db, t);
			return task;
		}

		public async Task<IEnumerable<BackgroundTask>> GetHangingTasksAsync()
		{
			var db = GetDbConnection();
			IEnumerable<BackgroundTask> locked;
			using (var t = BeginTransaction(db, out _))
				locked = await GetLockedTasksWithTagsAsync(db, t);

			return locked.Where(st => st.IsRunningOvertime(this)).ToList();
		}

		public async Task<IEnumerable<BackgroundTask>> GetAllAsync()
		{
			var db = GetDbConnection();
			using var t = BeginTransaction(db, out _);
			return await GetAllWithTagsAsync(db, t);
		}

		public async Task<bool> SaveAsync(BackgroundTask task)
		{
			var db = GetDbConnection();
			using (var t = BeginTransaction(db, out var owner))
			{
				if (task.Id == 0)
				{
					await InsertBackgroundTaskAsync(task, db, t);
				}
				else
				{
					await UpdateBackgroundTaskAsync(task, db, t);
				}

				await UpdateTagMappingAsync(task, db, t);

				if (owner)
					t.Commit();
			}

			return true;
		}

		public async Task<bool> DeleteAsync(BackgroundTask task)
		{
			var db = GetDbConnection();
			using (var t = BeginTransaction(db, out var owner))
			{
				var sql = $@"
-- Primary relationship:
DELETE FROM {TagsTable} WHERE BackgroundTaskId = :Id;
DELETE FROM {TaskTable} WHERE Id = :Id;

-- Remove any orphaned tags:
DELETE FROM {TagTable}
WHERE NOT EXISTS (SELECT 1 FROM {TagsTable} st WHERE {TagTable}.Id = st.TagId)
";
				await db.ExecuteAsync(sql, task, t);
				if (owner)
					t.Commit();
			}

			return true;
		}

		public DateTimeOffset GetTaskTimestamp()
		{
			return _timestamps().ToUniversalTime();
		}

		private IDbTransaction BeginTransaction(IDbConnection db, out bool owner)
		{
			var connection = GetDataConnection();
			var transaction = db.BeginTransaction(IsolationLevel.Serializable);
			connection.SetTransaction(transaction);
			owner = true;
			return transaction;
		}

		private async Task InsertBackgroundTaskAsync(BackgroundTask task, IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
INSERT INTO {TaskTable} 
    (Priority, Attempts, Handler, RunAt, MaximumRuntime, MaximumAttempts, DeleteOnSuccess, DeleteOnFailure, DeleteOnError, Expression, Start, [End], ContinueOnSuccess, ContinueOnFailure, ContinueOnError) 
VALUES
    (:Priority, :Attempts, :Handler, :RunAt, :MaximumRuntime, :MaximumAttempts, :DeleteOnSuccess, :DeleteOnFailure, :DeleteOnError, :Expression, :Start, :End, :ContinueOnSuccess, :ContinueOnFailure, :ContinueOnError);

SELECT MAX(Id) FROM {TaskTable};
";
			task.Id = (await db.QueryAsync<int>(sql, task, t)).Single();
			var createdAtString =
				await db.QuerySingleAsync<string>($"SELECT \"CreatedAt\" FROM {TaskTable} WHERE \"Id\" = :Id",
					new {task.Id}, t);
			task.CreatedAt = DateTimeOffset.Parse(createdAtString);
		}

		private async Task UpdateBackgroundTaskAsync(BackgroundTask task, IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
UPDATE {TaskTable} 
SET 
    Priority = :Priority, 
    Attempts = :Attempts, 
    Handler = :Handler, 
    RunAt = :RunAt, 
    MaximumRuntime = :MaximumRuntime, 
    MaximumAttempts = :MaximumAttempts, 
    DeleteOnSuccess = :DeleteOnSuccess,
    DeleteOnFailure = :DeleteOnFailure,
    DeleteOnError = :DeleteOnError,
    Expression = :Expression, 
    Start = :Start, 
    [End] = :End,
    ContinueOnSuccess = :ContinueOnSuccess,
    ContinueOnFailure = :ContinueOnFailure,
    ContinueOnError = :ContinueOnError,
    LastError = :LastError,
    FailedAt = :FailedAt, 
    SucceededAt = :SucceededAt, 
    LockedAt = :LockedAt, 
    LockedBy = :LockedBy
WHERE 
    Id = @Id
";
			await db.ExecuteAsync(sql, task, t);
		}

		private async Task LockTasksAsync(IReadOnlyCollection<BackgroundTask> tasks, IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
UPDATE {TaskTable}  
SET 
    LockedAt = :Now, 
    LockedBy = :User 
WHERE Id IN 
    :Ids
";
			var now = GetTaskTimestamp();
			var user = LockedIdentity.Get();

			await db.ExecuteAsync(sql, new {Now = now, Ids = tasks.Select(task => task.Id), User = user}, t);

			foreach (var task in tasks)
			{
				task.LockedAt = now;
				task.LockedBy = user;
			}
		}

		private async Task<IEnumerable<BackgroundTask>> GetLockedTasksWithTagsAsync(IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE 
    {TaskTable}.LockedAt IS NOT NULL
ORDER BY
    {TagTable}.Name ASC    
";
			return await QueryWithSplitOnTagsAsync(db, t, sql);
		}

		private async Task<List<BackgroundTask>> GetUnlockedTasksWithTagsAsync(int readAhead, IDbConnection db,
			IDbTransaction t)
		{
			// None locked, failed or succeeded, must be due, ordered by due time then priority
			var sql = $@"
SELECT st.* FROM {TaskTable} st
WHERE
    st.LockedAt IS NULL 
AND
    st.FailedAt IS NULL 
AND 
    st.SucceededAt IS NULL
AND 
    (st.RunAt <= DATETIME('now'))
ORDER BY 
    st.RunAt, 
    st.Priority ASC
LIMIT {readAhead}
";
			var matchSql = string.Format(sql, readAhead);

			var matches = (await db.QueryAsync<BackgroundTask>(matchSql, transaction: t)).ToList();

			if (!matches.Any())
			{
				return matches;
			}

			var fetchSql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id IN :Ids
ORDER BY {TagTable}.Name ASC
";
			var ids = matches.Select(m => m.Id);

			return await QueryWithSplitOnTagsAsync(db, t, fetchSql, new {Ids = ids});
		}

		private async Task<BackgroundTask> GetByIdWithTagsAsync(int id, IDbConnection db, IDbTransaction t)
		{
			var taskTable = TaskTable;
			var tagTable = TagTable;
			var tagsTable = TagsTable;

			var sql = $@"
SELECT {taskTable}.*, {tagTable}.Name FROM {taskTable}
LEFT JOIN {tagsTable} ON {tagsTable}.BackgroundTaskId = {taskTable}.Id
LEFT JOIN {tagTable} ON {tagsTable}.TagId = {tagTable}.Id
WHERE {taskTable}.Id = :Id
ORDER BY {tagTable}.Name ASC
";
			BackgroundTask task = null;
			await db.QueryAsync<BackgroundTask, string, BackgroundTask>(sql, (s, tag) =>
			{
				task ??= s;
				if (tag != null)
				{
					task.Tags.Add(tag);
				}

				return task;
			}, new {Id = id}, splitOn: "Name", transaction: t);

			return task;
		}

		private async Task<IList<BackgroundTask>> GetAllWithTagsAsync(IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
ORDER BY {TagTable}.Name ASC
";
			return await QueryWithSplitOnTagsAsync(db, t, sql);
		}

		private async Task<IList<BackgroundTask>> GetByAnyTagsWithTagsAsync(IReadOnlyCollection<string> tags,
			IDbConnection db, IDbTransaction t = null)
		{
			var matchSql = $@"
SELECT st.*
FROM {TagsTable} stt, {TaskTable} st, {TagTable} t
WHERE stt.TagId = t.Id
AND t.Name IN :Tags
AND st.Id = stt.BackgroundTaskId
GROUP BY 
st.[Priority], st.Attempts, st.Handler, st.RunAt, st.MaximumRuntime, st.MaximumAttempts, st.DeleteOnSuccess, st.DeleteOnFailure, st.DeleteOnError, st.Expression, st.Start, st.End, st.ContinueOnSuccess, st.ContinueOnFailure, st.ContinueOnError, 
st.Id, st.LastError, st.FailedAt, st.SucceededAt, st.LockedAt, st.LockedBy, st.CreatedAt,
t.Name
";
			var matches = db.Query<BackgroundTask>(matchSql, new {Tags = tags}, t).ToList();

			if (!matches.Any())
			{
				return matches;
			}

			var fetchSql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id IN :Ids
ORDER BY {TagTable}.Name ASC
";
			var ids = matches.Select(m => m.Id);

			return await QueryWithSplitOnTagsAsync(db, t, fetchSql, new {Ids = ids});
		}

		private async Task<IList<BackgroundTask>> GetByAllTagsWithTagsAsync(IReadOnlyCollection<string> tags,
			IDbConnection db, IDbTransaction t)
		{
			var matchSql = $@"
SELECT st.*
FROM {TagsTable} stt, {TaskTable} st, {TagTable} t
WHERE stt.TagId = t.Id
AND t.Name IN :Tags
AND st.Id = stt.BackgroundTaskId
GROUP BY 
st.[Priority], st.Attempts, st.Handler, st.RunAt, st.MaximumRuntime, st.MaximumAttempts, st.DeleteOnSuccess, st.DeleteOnFailure, st.DeleteOnError, st.Expression, st.Start, st.[End], st.ContinueOnSuccess, st.ContinueOnFailure, st.ContinueOnError, 
st.Id, st.LastError, st.FailedAt, st.SucceededAt, st.LockedAt, st.LockedBy, st.CreatedAt
HAVING COUNT (st.Id) = :Count
";
			var matches = (await db.QueryAsync<BackgroundTask>(matchSql, new {Tags = tags, tags.Count}, t)).ToList();

			if (!matches.Any())
			{
				return matches;
			}

			var fetchSql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id IN :Ids
ORDER BY {TagTable}.Name ASC
";
			var ids = matches.Select(m => m.Id);

			return await QueryWithSplitOnTagsAsync(db, t, fetchSql, new {Ids = ids});
		}

		private static async Task<List<BackgroundTask>> QueryWithSplitOnTagsAsync(IDbConnection db, IDbTransaction t,
			string sql,
			object data = null)
		{
			var lookup = new Dictionary<int, BackgroundTask>();
			await db.QueryAsync<BackgroundTask, string, BackgroundTask>(sql, (s, tag) =>
			{
				if (!lookup.TryGetValue(s.Id, out var task))
				{
					lookup.Add(s.Id, task = s);
				}

				if (tag != null)
				{
					task.Tags.Add(tag);
				}

				return task;
			}, data, splitOn: "Name", transaction: t);

			var result = lookup.Values.ToList();
			return result;
		}

		private async Task UpdateTagMappingAsync(BackgroundTask task, IDbConnection db, IDbTransaction t)
		{
			var source = task.Tags ?? NoTags;

			await db.ExecuteAsync($"DELETE FROM {TagsTable} WHERE BackgroundTaskId = @Id", task, t);

			if (source == NoTags || source.Count == 0)
				return;

			// normalize for storage
			var normalized = source.Select(st => st.Trim().Replace(" ", "-").Replace("'", "\""));

			foreach (var tags in normalized.Split(1000))
			{
				foreach (var tag in tags)
				{
					await db.ExecuteAsync($@"INSERT OR IGNORE INTO {TagTable} (Name) VALUES (:Name);", new {Name = tag},
						t);

					var id = db.QuerySingle<int>($"SELECT Id FROM {TagTable} WHERE Name = :Name", new {Name = tag}, t);

					await db.ExecuteAsync(
						$@"INSERT INTO {TagsTable} (BackgroundTaskId, TagId) VALUES (:BackgroundTaskId, :TagId)",
						new {BackgroundTaskId = task.Id, TagId = id}, t);
				}
			}
		}

		private IDbConnection GetDbConnection()
		{
			// IMPORTANT: DI is out of our hands, here.
			var connection = GetDataConnection();

			var current = connection.Current;

			if (_lastDbConnection != null && _lastDbConnection.TryGetTarget(out var target) && target == current)
				_logger.Debug(() => "IDbConnection is pre-initialized.");

			_lastDbConnection = new WeakReference<IDbConnection>(current, false);

			return current;
		}

		private IDataConnection<BackgroundTaskBuilder> GetDataConnection()
		{
			// IMPORTANT: DI is out of our hands, here.
			var connection = _serviceProvider.GetRequiredService<IDataConnection<BackgroundTaskBuilder>>();

			if (_lastDataConnection != null && _lastDataConnection.TryGetTarget(out var target) && target == connection)
				_logger.Debug(() => "IDataConnection is pre-initialized.");

			_lastDataConnection = new WeakReference<IDataConnection>(connection, false);

			return connection;
		}
	}
}