// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;
using ActiveScheduler.SqlServer.Internal;
using ActiveScheduler.SqlServer.Internal.SessionManagement;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static TypeKitchen.EnumerableExtensions;

namespace ActiveScheduler.SqlServer
{
	public class SqlServerBackgroundTaskStore : IBackgroundTaskStore
	{
		private static readonly IReadOnlyList<string> NoTags = new List<string>();

		private readonly ISafeLogger<SqlServerBackgroundTaskStore> _logger;
		private readonly IOptionsMonitor<BackgroundTaskOptions> _options;
		private readonly string _schema;
		private readonly IServiceProvider _serviceProvider;
		private readonly string _tablePrefix;
		private readonly Func<DateTimeOffset> _timestamps;

		private WeakReference<IDataConnection> _lastDataConnection;
		private WeakReference<IDbConnection> _lastDbConnection;

		public SqlServerBackgroundTaskStore(IServiceProvider serviceProvider,
			Func<DateTimeOffset> timestamps,
			IOptionsMonitor<BackgroundTaskOptions> options,
			string schema = "dbo",
			string tablePrefix = nameof(BackgroundTask),
			ISafeLogger<SqlServerBackgroundTaskStore> logger = null)
		{
			_serviceProvider = serviceProvider;
			_timestamps = timestamps;
			_options = options;
			_schema = schema;
			_tablePrefix = tablePrefix;
			_logger = logger;
		}

		internal string TaskTable => $"[{_schema}].[{_tablePrefix}]";
		internal string TagTable => $"[{_schema}].[{_tablePrefix}_Tag]";
		internal string TagsTable => $"[{_schema}].[{_tablePrefix}_Tags]";

		public DateTimeOffset GetTaskTimestamp()
		{
			return _timestamps().ToUniversalTime();
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

		private static IEnumerable<int> GetTaskIds(List<BackgroundTask> tasks)
		{
			foreach (var id in tasks.Enumerate(x => x.Id))
				yield return id;
		}

		private static IEnumerable<Guid> GetTaskCorrelationIds(List<BackgroundTask> tasks)
		{
			foreach (var id in tasks.Enumerate(x => x.CorrelationId))
				yield return id;
		}

		private static IEnumerable<string> GetTagClauses(List<string> tags)
		{
			foreach (var tag in tags.Enumerate(tag => $"('{tag}')"))
				yield return tag;
		}

		#region Query

		public Task<IEnumerable<BackgroundTask>> GetByAnyTagsAsync(params string[] tags)
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);
			try
			{
				var tasks = GetByAnyTagsWithTags(tags, db, t);
				return Task.FromResult(tasks);
			}
			finally
			{
				if (owner)
					CommitTransaction(t);
			}
		}

		public Task<IEnumerable<BackgroundTask>> GetByAllTagsAsync(params string[] tags)
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);
			try
			{
				var tasks = GetByAllTagsWithTags(tags, db, t);
				return Task.FromResult(tasks);
			}
			finally
			{
				if (owner)
					CommitTransaction(t);
			}
		}

		public Task<BackgroundTask> GetByIdAsync(int id)
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);
			try
			{
				var task = GetByIdWithTags(id, db, t);
				return Task.FromResult(task);
			}
			finally
			{
				if (owner)
					CommitTransaction(t);
			}
		}

		public Task<IEnumerable<BackgroundTask>> GetHangingTasksAsync()
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);
			try
			{
				var locked = GetLockedTasksWithTags(db, t);
				var tasks = locked.Where(st => st.IsRunningOvertime(this));
				return Task.FromResult(tasks);
			}
			finally
			{
				if (owner)
					CommitTransaction(t);
			}
		}

		public Task<IEnumerable<BackgroundTask>> GetAllAsync()
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);
			try
			{
				var tasks = GetAllWithTags(db, t);
				return Task.FromResult(tasks);
			}
			finally
			{
				if (owner)
					CommitTransaction(t);
			}
		}

		#endregion

		#region Mutate

		public Task<bool> SaveAsync(BackgroundTask task)
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);

			var success = false;

			try
			{
				if (task.Id == default)
					InsertBackgroundTask(task, db, t);
				else
					UpdateBackgroundTask(task, db, t);

				UpdateTagMapping(task, db, t);

				success = true;
			}
			catch (Exception e)
			{
				_logger.Error(() => "Error saving task with ID {Id}", e, task.Id);
			}
			finally
			{
				if (owner)
				{
					if (success)
						CommitTransaction(t);
					else
						RollbackTransaction(t);
				}
			}

			return Task.FromResult(success);
		}

		public Task<bool> DeleteAsync(BackgroundTask task)
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);

			var success = false;

			try
			{
				var sql = $@"
-- Primary relationship:
DELETE FROM {TagsTable} WHERE BackgroundTaskId = @Id;
DELETE FROM {TaskTable} WHERE Id = @Id;

-- Remove any orphaned tags:
DELETE FROM {TagTable}
WHERE NOT EXISTS (SELECT 1 FROM {TagsTable} st WHERE {TagTable}.Id = st.TagId)
";
				var deleted = db.Execute(sql, task, t);
				if (deleted != 1)
					_logger.Warn(() => "Task with ID {Id} was not deleted", task.Id);

				success = true;
			}
			catch (Exception e)
			{
				_logger.Error(() => "Error deleting task with ID {Id}", e, task.Id);
			}
			finally
			{
				if (owner)
				{
					if (success)
						CommitTransaction(t);
					else
						RollbackTransaction(t);
				}
			}

			return Task.FromResult(success);
		}

		public Task<IEnumerable<BackgroundTask>> LockNextAvailableAsync(int readAhead)
		{
			var db = GetDbConnection();
			var t = BeginTransaction((SqlConnection) db, out var owner);

			var success = false;

			try
			{
				var tasks = GetUnlockedTasksWithTags(readAhead, db, t);

				if (tasks.Count > 0)
					LockTasks(tasks, db, t);

				var result = Task.FromResult((IEnumerable<BackgroundTask>) tasks);
				success = true;
				return result;
			}
			catch (Exception e)
			{
				_logger.Error(() => "Error locking tasks", e);
				return Task.FromResult(Enumerable.Empty<BackgroundTask>());
			}
			finally
			{
				if (owner)
				{
					if (success)
						CommitTransaction(t);
					else
						RollbackTransaction(t);
				}
			}
		}

		#endregion

		#region Transaction Management

		private static readonly object Sync = new object();

		private IDbTransaction BeginTransaction(SqlConnection connection, out bool owner,
			[CallerMemberName] string callerMemberName = null)
		{
			var db = GetDataConnection();

			if (db.Transaction == null)
			{
				lock (Sync)
				{
					if (db.Transaction == null)
					{
						Trace.TraceInformation(
							$"{callerMemberName}:{nameof(BeginTransaction)} on {Thread.CurrentThread.ManagedThreadId}");
						var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
						db.SetTransaction(transaction);
						owner = true;

						_logger?.Debug(() => "Owner-initiated transaction occurred.");
						return transaction;
					}
				}
			}

			_logger?.Debug(() => "Transaction is already initiated; we are not the owner.");
			owner = false;
			return db.Transaction;
		}

		private void CommitTransaction(IDbTransaction t, [CallerMemberName] string callerMemberName = null)
		{
			t.Commit();
			var db = GetDataConnection();
			db.SetTransaction(null);
			_logger?.Debug(() => "Owner-transaction committed.");
			Trace.TraceInformation(
				$"{callerMemberName}:{nameof(CommitTransaction)} on {Thread.CurrentThread.ManagedThreadId}");
		}

		private void RollbackTransaction(IDbTransaction t, [CallerMemberName] string callerMemberName = null)
		{
			t.Rollback();
			var db = GetDataConnection();
			db.SetTransaction(null);
			_logger?.Debug(() => "Owner-transaction rolled back.");
			Trace.TraceInformation(
				$"{callerMemberName}:{nameof(RollbackTransaction)} on {Thread.CurrentThread.ManagedThreadId}");
		}

		#endregion

		#region Transacted Calls (Non-Async)

		private IEnumerable<BackgroundTask> GetLockedTasksWithTags(IDbConnection db, IDbTransaction t)
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
			return QueryWithSplitOnTags(db, t, sql);
		}

		private IList<BackgroundTask> GetUnlockedTasksWithTags(int readAhead, IDbConnection db, IDbTransaction t)
		{
			var now = GetTaskTimestamp();

			string sql;
			if (_options.CurrentValue.Store.FilterCorrelatedTasks)
			{
				// None locked, failed or succeeded, must be due, ordered by due time then priority,
				// from a set correlated by continuous task (only the latest due task is included)
				sql = $@"
WITH Correlated AS
(
    SELECT st.*, ROW_NUMBER() OVER(PARTITION BY st.[CorrelationId] ORDER BY st.[RunAt] DESC) AS [Row]
    FROM {TaskTable} st
)
SELECT TOP {readAhead} c.* FROM Correlated c
WHERE
    c.[Row] = 1
AND
    c.[LockedAt] IS NULL 
AND
    c.[FailedAt] IS NULL 
AND 
    c.[SucceededAt] IS NULL
AND 
    (c.[RunAt] <= @Now)
ORDER BY 
    c.[RunAt], 
    c.[Priority] ASC
";
			}
			else
			{
				// None locked, failed or succeeded, must be due, ordered by due time then priority
				sql = $@"
SELECT TOP {readAhead} st.* FROM {TaskTable} st
WHERE
    st.[LockedAt] IS NULL 
AND
    st.[FailedAt] IS NULL 
AND 
    st.[SucceededAt] IS NULL
AND 
    (st.[RunAt] <= @Now)
ORDER BY 
    st.[RunAt], 
    st.[Priority] ASC
";
			}

			var matches = db.Query<BackgroundTask>(sql, new {Now = now}, t).AsList();
			if (matches.Count == 0)
			{
				_logger.Debug(() => "No unlocked tasks found.");
				return matches;
			}

			var fetchSql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id IN @Ids
ORDER BY {TagTable}.Name ASC
";

			var ids = GetTaskIds(matches);
			var tasks = QueryWithSplitOnTags(db, t, fetchSql, new {Ids = ids});
			return tasks;
		}

		private BackgroundTask GetByIdWithTags(int id, IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id = @Id
ORDER BY {TagTable}.Name ASC
";
			BackgroundTask task = null;
			db.Query<BackgroundTask, string, BackgroundTask>(sql, (s, tag) =>
			{
				task ??= s;
				if (tag != null)
					task.Tags.Add(tag);
				return task;
			}, new {Id = id}, splitOn: "Name", transaction: t);

			return task;
		}

		private IEnumerable<BackgroundTask> GetAllWithTags(IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
ORDER BY {TagTable}.Name ASC
";
			var tasks = QueryWithSplitOnTags(db, t, sql);
			return tasks;
		}

		private IEnumerable<BackgroundTask> GetByAnyTagsWithTags(IEnumerable tags, IDbConnection db, IDbTransaction t)
		{
			var matchSql = $@"
SELECT st.*
FROM {TagsTable} stt, {TaskTable} st, {TagTable} t
WHERE stt.TagId = t.Id
AND t.Name IN @Tags
AND st.Id = stt.BackgroundTaskId
GROUP BY 
st.CorrelationId, st.[Priority], st.Attempts, st.Handler, st.RunAt, st.MaximumRuntime, st.MaximumAttempts, st.DeleteOnSuccess, st.DeleteOnFailure, st.DeleteOnError, st.Expression, st.Start, st.[End], st.ContinueOnSuccess, st.ContinueOnFailure, st.ContinueOnError, st.Data,
st.Id, st.LastError, st.FailedAt, st.SucceededAt, st.LockedAt, st.LockedBy, st.CreatedAt,
t.Name
";
			var matches = db.Query<BackgroundTask>(matchSql, new {Tags = tags}, t).AsList();
			if (matches.Count == 0)
				return matches;

			var fetchSql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id IN @Ids
ORDER BY {TagTable}.Name ASC
";

			var ids = GetTaskIds(matches);
			var tasks = QueryWithSplitOnTags(db, t, fetchSql, new {Ids = ids}).AsList();
			return tasks;
		}

		private IEnumerable<BackgroundTask> GetByAllTagsWithTags(IReadOnlyCollection<string> tags, IDbConnection db,
			IDbTransaction t)
		{
			var matchSql = $@"
SELECT st.*
FROM {TagsTable} stt, {TaskTable} st, {TagTable} t
WHERE stt.TagId = t.Id
AND t.Name IN @Tags
AND st.Id = stt.BackgroundTaskId
GROUP BY 
st.CorrelationId, st.[Priority], st.Attempts, st.Handler, st.RunAt, st.MaximumRuntime, st.MaximumAttempts, st.DeleteOnSuccess, st.DeleteOnFailure, st.DeleteOnError, st.Expression, st.Start, st.[End], st.ContinueOnSuccess, st.ContinueOnFailure, st.ContinueOnError, st.Data,
st.Id, st.LastError, st.FailedAt, st.SucceededAt, st.LockedAt, st.LockedBy, st.CreatedAt
HAVING COUNT (st.Id) = @Count
";

			var matches = db.Query<BackgroundTask>(matchSql, new {Tags = tags, tags.Count}, t).AsList();
			if (matches.Count == 0)
				return matches;

			var fetchSql = $@"
SELECT {TaskTable}.*, {TagTable}.Name FROM {TaskTable}
LEFT JOIN {TagsTable} ON {TagsTable}.BackgroundTaskId = {TaskTable}.Id
LEFT JOIN {TagTable} ON {TagsTable}.TagId = {TagTable}.Id
WHERE {TaskTable}.Id IN @Ids
ORDER BY {TagTable}.Name ASC
";

			var ids = GetTaskIds(matches);
			var tasks = QueryWithSplitOnTags(db, t, fetchSql, new {Ids = ids}).AsList();
			return tasks;
		}

		private static IList<BackgroundTask> QueryWithSplitOnTags(IDbConnection db, IDbTransaction t, string sql,
			object data = null)
		{
			var lookup = new Dictionary<int, BackgroundTask>();

			db.Query<BackgroundTask, string, BackgroundTask>(sql, (s, tag) =>
			{
				if (!lookup.TryGetValue(s.Id, out var task))
					lookup.Add(s.Id, task = s);
				if (tag != null)
					task.Tags.Add(tag);
				return task;
			}, data, splitOn: "Name", transaction: t);

			var result = lookup.Values.ToList();
			return result;
		}

		private void UpdateBackgroundTask(BackgroundTask task, IDbConnection db, IDbTransaction t)
		{
			var sql = $@"
UPDATE {TaskTable} 
SET 
    Priority = @Priority, 
    Attempts = @Attempts, 
    Handler = @Handler, 
    RunAt = @RunAt, 
    MaximumRuntime = @MaximumRuntime, 
    MaximumAttempts = @MaximumAttempts, 
    DeleteOnSuccess = @DeleteOnSuccess,
    DeleteOnFailure = @DeleteOnFailure,
    DeleteOnError = @DeleteOnError,
    Expression = @Expression, 
    Start = @Start, 
    [End] = @End,
    ContinueOnSuccess = @ContinueOnSuccess,
    ContinueOnFailure = @ContinueOnFailure,
    ContinueOnError = @ContinueOnError,
    LastError = @LastError,
    FailedAt = @FailedAt, 
    SucceededAt = @SucceededAt, 
    LockedAt = @LockedAt, 
    LockedBy = @LockedBy
WHERE 
    Id = @Id
";
			db.Execute(sql, task, t);
		}

		private void InsertBackgroundTask(BackgroundTask task, IDbConnection db, IDbTransaction t)
		{
			task.CreatedAt = GetTaskTimestamp();

			var sql = $@"
INSERT INTO {TaskTable} 
    (CorrelationId, Priority, Attempts, Handler, RunAt, MaximumRuntime, MaximumAttempts, DeleteOnSuccess, DeleteOnFailure, DeleteOnError, Expression, Start, [End], ContinueOnSuccess, ContinueOnFailure, ContinueOnError, [Data], [CreatedAt]) 
VALUES
    (@CorrelationId, @Priority, @Attempts, @Handler, @RunAt, @MaximumRuntime, @MaximumAttempts, @DeleteOnSuccess, @DeleteOnFailure, @DeleteOnError, @Expression, @Start, @End, @ContinueOnSuccess, @ContinueOnFailure, @ContinueOnError, @Data, @CreatedAt);

SELECT MAX([Id]) AS [Id] FROM {TaskTable}
";
			task.Id = db.QuerySingle<int>(sql, task, t);
		}

		private void LockTasks(IEnumerable<BackgroundTask> tasks, IDbConnection db, IDbTransaction t)
		{
			var now = GetTaskTimestamp();
			var user = LockedIdentity.Get();

			var matches = tasks.AsList();
			var ids = GetTaskIds(matches).AsList();

			if (_options.CurrentValue.Store.FilterCorrelatedTasks)
				FilterCorrelatedTasks(db, t, now, ids, matches);

			var lockTasks = $@"
UPDATE {TaskTable}  
SET 
    LockedAt = @Now, 
    LockedBy = @User 
WHERE Id IN 
    @Ids;
";

			_logger.Debug(() => "Locking {TaskCount} tasks", matches.Count);
			var updated = db.Execute(lockTasks, new {Now = now, Ids = ids, User = user}, t);
			if (updated != matches.Count)
				_logger.Warn(() => "Did not lock the expected number of tasks");

			foreach (var task in matches)
			{
				task.LockedAt = now;
				task.LockedBy = user;
			}
		}

		private void FilterCorrelatedTasks(IDbConnection db, IDbTransaction t, DateTimeOffset now, List<int> ids,
			List<BackgroundTask> matches)
		{
			_logger.Debug(() => "Evicting correlated tasks");

			var filterTasks = $@"
UPDATE {TaskTable}
SET
	[LastError] = 'Evicted by correlation',
	[FailedAt] = @Now,
	[LockedAt] = @Now,
	[LockedBy] = '{Constants.Scheduling.EvictionUser}'
WHERE
    [LockedAt] IS NULL 
AND
    [FailedAt] IS NULL 
AND 
    [SucceededAt] IS NULL
AND
	[CorrelationId] IN @CorrelationIds
AND
	[Id] NOT IN @Ids
";
			var filtered = db.Execute(filterTasks,
				new {Now = now, Ids = ids, CorrelationIds = GetTaskCorrelationIds(matches)}, t);
			if (filtered > 0)
				_logger.Debug(() => "Evicted {Filtered} correlated tasks", filtered);
		}

		private void UpdateTagMapping(BackgroundTask task, IDbConnection db, IDbTransaction t)
		{
			var source = task.Tags ?? NoTags;

			if (Equals(source, NoTags) || source.Count == 0)
			{
				db.Execute($"DELETE FROM {TagsTable} WHERE [BackgroundTaskId] = @Id", task, t);
				return;
			}

			// normalize for storage
			var normalized = source.Select(st => st.Trim().Replace(" ", "-").Replace("'", "\""));

			// values = VALUES @Tags
			// sql server only permits 1000 values per statement
			foreach (var tags in normalized.Split(1000))
			{
				var upsertSql = $@"
-- upsert tags
MERGE {TagTable}
USING (VALUES {{0}}) AS Pending (Name) 
ON {TagTable}.Name = Pending.Name 
WHEN NOT MATCHED THEN
    INSERT (Name) VALUES (Pending.Name)
;

-- sync tag mappings
MERGE {TagsTable}
USING
    (SELECT @BackgroundTaskId AS BackgroundTaskId, Id AS TagId 
       FROM {TagTable} 
      WHERE Name IN @Tags) AS Pending (BackgroundTaskId, TagId) 
ON {TagsTable}.BackgroundTaskId = Pending.BackgroundTaskId 
AND {TagsTable}.TagId = Pending.TagId 
WHEN NOT MATCHED BY SOURCE AND {TagsTable}.BackgroundTaskId = @BackgroundTaskId THEN
    DELETE 
WHEN NOT MATCHED THEN 
    INSERT (BackgroundTaskId,TagId) VALUES (Pending.BackgroundTaskId, Pending.TagId)
;
";
				var values = GetTagClauses(tags);
				var sql = string.Format(upsertSql, string.Join(",", values));
				db.Execute(sql, new {BackgroundTaskId = task.Id, Tags = tags}, t);
			}
		}

		#endregion
	}
}