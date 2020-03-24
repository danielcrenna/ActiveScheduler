// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.IO;
using ActiveConnection;
using FluentMigrator.Runner;

namespace ActiveScheduler.SqlServer
{
	internal sealed class SqlServerMigrationRunner : DbMigrationRunner
	{
		public SqlServerMigrationRunner(string connectionString) : base(connectionString) { }

		public override void CreateDatabaseIfNotExists()
		{
			var builder = new SqlConnectionStringBuilder(ConnectionString);
			if (File.Exists(builder.InitialCatalog))
				return;
			var connection = new SqlConnection(builder.ConnectionString);
			connection.Open();
			connection.Close();
		}

		public override void ConfigureRunner(IMigrationRunnerBuilder builder) => builder.AddSqlServer();
	}
}