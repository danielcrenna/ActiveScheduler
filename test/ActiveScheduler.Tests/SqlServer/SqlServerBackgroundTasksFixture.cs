// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveScheduler.SqlServer;
using ActiveScheduler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.Tests.SqlServer
{
	public class SqlServerBackgroundTasksFixture : SqlServerFixture
	{
		public override void ConfigureServices(IServiceCollection services)
		{
			services.AddBackgroundTasks(o => { })
				.AddSqlServerBackgroundTasksStore(ConnectionString, r => DateTimeOffset.Now);
		}
	}
}