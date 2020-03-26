// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveConnection;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;
using ActiveScheduler.SqlServer.Internal;
using ActiveScheduler.SqlServer.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.SqlServer
{
	public static class Add
	{
		public static BackgroundTaskBuilder AddSqlServerBackgroundTasksStore(this BackgroundTaskBuilder builder,
			string connectionString, Func<IServiceProvider, DateTimeOffset> timestamps, ConnectionScope scope = ConnectionScope.ByThread)
		{
			if (scope == ConnectionScope.ByRequest)
				builder.Services.AddHttpContextAccessor();

			builder.Services.AddSafeLogging();
			builder.Services.AddDatabaseConnection<BackgroundTaskBuilder, SqlServerConnectionFactory>(connectionString, scope);
			builder.Services.Replace(ServiceDescriptor.Singleton<IBackgroundTaskStore, SqlServerBackgroundTaskStore>(r => 
				new SqlServerBackgroundTaskStore(r, () => timestamps(r),
					r.GetRequiredService<IOptionsMonitor<BackgroundTaskOptions>>(),
					logger: r.GetService<ISafeLogger<SqlServerBackgroundTaskStore>>())));

			var serviceProvider = builder.Services.BuildServiceProvider();
			var options = serviceProvider.GetRequiredService<IOptions<BackgroundTaskOptions>>();
			MigrateToLatest(connectionString, options.Value);

			return builder;
		}

		private static void MigrateToLatest(string connectionString, BackgroundTaskOptions options)
		{
			var runner = new SqlServerMigrationRunner<SqlServerConnectionOptions>(connectionString, 
				new SqlServerConnectionOptions(options.Store));

			runner.OnStartAsync<CreateBackgroundTasksSchema>().GetAwaiter().GetResult();
		}
	}
}