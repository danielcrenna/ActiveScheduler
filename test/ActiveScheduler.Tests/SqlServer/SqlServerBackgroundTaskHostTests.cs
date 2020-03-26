// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using Xunit.Abstractions;

namespace ActiveScheduler.Tests.SqlServer
{
	public class SqlServerBackgroundTaskHostTests : BackgroundTaskHostTests,
		IClassFixture<SqlServerBackgroundTasksFixture>
	{
		private readonly SqlServerBackgroundTasksFixture _fixture;

		// ReSharper disable once SuggestBaseTypeForParameter
		public SqlServerBackgroundTaskHostTests(SqlServerBackgroundTasksFixture fixture, ITestOutputHelper console) :
			base(CreateServiceProvider(fixture), console) => _fixture = fixture;
	}
}