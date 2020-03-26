// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using ActiveConnection;
using ActiveScheduler.Tests.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.Tests.Fixtures
{
	public abstract class SqliteFixture : IServiceFixture
	{
		public IServiceProvider ServiceProvider { get; set; }

		public virtual void ConfigureServices(IServiceCollection services) { }

		public void Dispose()
		{
			var connection = ServiceProvider?.GetRequiredService<IDataConnection<BackgroundTaskBuilder>>();
			if (!(connection?.Current is SqliteConnection sqlite))
				return;

			sqlite.Close();
			sqlite.Dispose();

			GC.Collect();
			GC.WaitForPendingFinalizers();

			if (sqlite.DataSource != null)
				File.Delete(sqlite.DataSource);
		}
	}
}