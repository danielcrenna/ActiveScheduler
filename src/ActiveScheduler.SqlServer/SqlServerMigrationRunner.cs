// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using ActiveScheduler.SqlServer.Internal;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.SqlServer
{
	public class SqlServerMigrationRunner
	{
		private readonly string _connectionString;

		public SqlServerMigrationRunner(string connectionString) => _connectionString = connectionString;

		public void CreateDatabaseIfNotExists()
		{
			var builder = new SqlConnectionStringBuilder(_connectionString);
			if (File.Exists(builder.InitialCatalog))
				return;
			var connection = new SqlConnection(builder.ConnectionString);
			connection.Open();
			connection.Close();
		}
		
		public void MigrateUp(Assembly assembly, string ns)
		{
			var container = new ServiceCollection()
				.AddFluentMigratorCore()
				.ConfigureRunner(
					builder =>
					{
						builder.AddSqlServer();
						builder
							.WithGlobalConnectionString(_connectionString)
							.ScanIn(assembly).For.Migrations();
					})
				.BuildServiceProvider();

			var runner = container.GetRequiredService<IMigrationRunner>();
			if (runner is MigrationRunner defaultRunner &&
			    defaultRunner.MigrationLoader is DefaultMigrationInformationLoader defaultLoader)
			{
				var source = container.GetRequiredService<IFilteringMigrationSource>();
				defaultRunner.MigrationLoader = new NamespaceMigrationInformationLoader(ns, source, defaultLoader);
			}

			runner.MigrateUp();
		}
	}
}