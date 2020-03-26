// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using ActiveConnection;
using FluentMigrator.Runner;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.SqlServer.Internal
{ 
	internal sealed class SqlServerMigrationRunner<TOptions> : DbMigrationRunner<TOptions> 
		where TOptions : class, IDbConnectionOptions, new()
	{
		public SqlServerMigrationRunner(string connectionString, IOptions<TOptions> options) : base(connectionString, options) { }
		
		public override async Task CreateDatabaseIfNotExistsAsync()
		{
			var builder = new SqlConnectionStringBuilder(ConnectionString);
			if (!File.Exists(builder.InitialCatalog))
			{
				var connection = new SqlConnection(builder.ConnectionString);
				await connection.OpenAsync();
				connection.Close();
			}
		}

		public override void ConfigureRunner(IMigrationRunnerBuilder builder) => builder.AddSqlServer();
	}
}