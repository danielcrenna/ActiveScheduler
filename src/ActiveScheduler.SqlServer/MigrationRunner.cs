// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using ActiveScheduler.SqlServer.Internal;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.SqlServer
{
	public abstract class MigrationRunner
	{
		protected readonly string ConnectionString;

		protected MigrationRunner(string connectionString) => ConnectionString = connectionString;

		public abstract void CreateDatabaseIfNotExists();

		public abstract void ConfigureMigrator(IMigrationRunnerBuilder builder);

		public void MigrateUp(Assembly assembly, string ns)
		{
			var container = new ServiceCollection()
				.AddFluentMigratorCore()
				.ConfigureRunner(
					builder =>
					{
						ConfigureMigrator(builder);
						builder
							.WithGlobalConnectionString(ConnectionString)
							.ScanIn(assembly).For.Migrations();
					})
				.BuildServiceProvider();

			var runner = container.GetRequiredService<IMigrationRunner>();
			if (runner is FluentMigrator.Runner.MigrationRunner defaultRunner &&
			    defaultRunner.MigrationLoader is DefaultMigrationInformationLoader defaultLoader)
			{
				var source = container.GetRequiredService<IFilteringMigrationSource>();
				defaultRunner.MigrationLoader = new NamespaceMigrationInformationLoader(ns, source, defaultLoader);
			}

			runner.MigrateUp();
		}
	}
}