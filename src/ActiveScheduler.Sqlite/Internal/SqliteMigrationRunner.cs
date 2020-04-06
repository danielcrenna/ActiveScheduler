// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using ActiveConnection;
using FluentMigrator.Runner;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.Sqlite.Internal
{
	internal sealed class SqliteMigrationRunner : DbMigrationRunner<SqliteConnectionOptions>
	{
		public SqliteMigrationRunner(string connectionString, IOptions<SqliteConnectionOptions> options) : base(connectionString, options) { }

		public override async Task CreateDatabaseIfNotExistsAsync()
		{
			var builder = new SqliteConnectionStringBuilder(ConnectionString) {Mode = SqliteOpenMode.ReadWriteCreate};
			if (File.Exists(builder.DataSource))
				return;
			var connection = new SqliteConnection(builder.ConnectionString);
			await connection.OpenAsync();
			connection.Close();
		}

		public override void ConfigureRunner(IMigrationRunnerBuilder builder)
		{
			builder.AddSQLite();
		}
	}
}