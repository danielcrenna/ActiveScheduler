// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ActiveScheduler.Models
{
	public interface IBackgroundTaskStore
	{
		Task<BackgroundTask> GetByIdAsync(int id);

		Task<IEnumerable<BackgroundTask>> GetAllAsync();
		Task<IEnumerable<BackgroundTask>> GetByAllTagsAsync(params string[] tags);
		Task<IEnumerable<BackgroundTask>> GetByAnyTagsAsync(params string[] tags);
		Task<IEnumerable<BackgroundTask>> GetHangingTasksAsync();

		Task<bool> SaveAsync(BackgroundTask task);
		Task<bool> DeleteAsync(BackgroundTask task);
		Task<IEnumerable<BackgroundTask>> LockNextAvailableAsync(int readAhead);

		DateTimeOffset GetTaskTimestamp();
	}
}