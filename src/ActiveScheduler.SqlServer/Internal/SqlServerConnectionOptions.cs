// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ActiveConnection;
using ActiveScheduler.Configuration;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.SqlServer.Internal
{
	public class SqlServerConnectionOptions : IDbConnectionOptions, IOptions<SqlServerConnectionOptions>
	{
		private readonly StoreOptions _options;

		public SqlServerConnectionOptions()
		{
		}


		public SqlServerConnectionOptions(StoreOptions options) => _options = options;

		public bool CreateIfNotExists => _options.CreateIfNotExists;
		public bool MigrateOnStartup => _options.MigrateOnStartup;

		public SqlServerConnectionOptions Value => this;
	}
}