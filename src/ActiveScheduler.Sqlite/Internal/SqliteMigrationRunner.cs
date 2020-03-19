// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using ActiveConnection;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;

namespace ActiveScheduler.Sqlite.Internal
{
	internal sealed class SqliteMigrationRunner : DbMigrationRunner
	{
		public SqliteMigrationRunner(string connectionString) : base(connectionString) { }

		public override void CreateDatabaseIfNotExists()
		{
			var builder = new SqliteConnectionStringBuilder(ConnectionString) {Mode = SqliteOpenMode.ReadWriteCreate};
			if (File.Exists(builder.DataSource))
				return;
			var connection = new SqliteConnection(builder.ConnectionString);
			connection.Open();
			connection.Close();
		}

		public override void ConfigureRunner(IMigrationRunnerBuilder builder)
		{
			builder.AddSQLite();
		}
	}
}