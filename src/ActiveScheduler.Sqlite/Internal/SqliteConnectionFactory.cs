// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using ActiveConnection;
using Microsoft.Data.Sqlite;

namespace ActiveScheduler.Sqlite.Internal
{
	internal sealed class SqliteConnectionFactory : IDbConnectionFactory
	{
		public string ConnectionString { get; set; }

		public IDbConnection CreateConnection()
		{
			return new SqliteConnection(ConnectionString);
		}
	}
}