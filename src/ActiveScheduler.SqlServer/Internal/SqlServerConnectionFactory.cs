// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.SqlClient;
using ActiveConnection;

namespace ActiveScheduler.SqlServer
{
	internal sealed class SqlServerConnectionFactory : IDbConnectionFactory
	{
		public string ConnectionString { get; set; }
		public IDbConnection CreateConnection() => new SqlConnection(ConnectionString);
	}
}