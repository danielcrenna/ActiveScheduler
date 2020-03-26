// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveConnection;
using ActiveScheduler.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.Tests.Fixtures
{
	public class SqliteBackgroundTasksFixture : SqliteFixture
	{
		public override void ConfigureServices(IServiceCollection services)
		{
			services.AddBackgroundTasks(o => { })
				.AddSqliteBackgroundTasksStore($"Data Source={Guid.NewGuid()}.db", ConnectionScope.KeepAlive);
		}
	}
}