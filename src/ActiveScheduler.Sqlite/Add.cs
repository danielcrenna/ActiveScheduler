// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ActiveConnection;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Models;
using ActiveScheduler.Sqlite.Internal;
using ActiveScheduler.Sqlite.Migrations;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.Sqlite
{
	public static class Add
	{
		public static BackgroundTaskBuilder AddSqliteBackgroundTasksStore(this BackgroundTaskBuilder builder,
			string connectionString, ConnectionScope scope = ConnectionScope.ByRequest)
		{
			if (scope == ConnectionScope.ByRequest)
				builder.Services.AddHttpContextAccessor();

			builder.Services.AddSafeLogging();
			builder.Services.AddDatabaseConnection<BackgroundTaskBuilder, SqliteConnectionFactory>(connectionString,
				scope);
			builder.Services.Replace(ServiceDescriptor.Singleton<IBackgroundTaskStore, SqliteBackgroundTaskStore>());

			SqlMapper.AddTypeHandler(DateTimeOffsetHandler.Default);
			SqlMapper.AddTypeHandler(TimeSpanHandler.Default);

			var serviceProvider = builder.Services.BuildServiceProvider();
			var options = serviceProvider.GetRequiredService<IOptions<BackgroundTaskOptions>>();
			MigrateToLatest(connectionString, options.Value);

			return builder;
		}

		private static void MigrateToLatest(string connectionString, BackgroundTaskOptions options)
		{
			var runner = new SqliteMigrationRunner(connectionString);

			if (options.Store.CreateIfNotExists)
			{
				runner.CreateDatabaseIfNotExists();
			}

			if (options.Store.MigrateOnStartup)
			{
				runner.MigrateUp(typeof(CreateBackgroundTasksSchema).Assembly,
					typeof(CreateBackgroundTasksSchema).Namespace);
			}
		}
	}
}