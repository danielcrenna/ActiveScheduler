// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace ActiveScheduler.Tests.SqlServer
{
	public class SqlServerBackgroundTaskStoreTests : BackgroundTaskStoreTests,
		IClassFixture<SqlServerBackgroundTasksFixture>
	{
		private readonly SqlServerBackgroundTasksFixture _fixture;

		// ReSharper disable once SuggestBaseTypeForParameter
		public SqlServerBackgroundTaskStoreTests(SqlServerBackgroundTasksFixture fixture) : base(
			CreateServiceProvider(fixture)) => _fixture = fixture;
	}
}