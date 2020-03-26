// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveScheduler.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActiveScheduler.Tests
{
	public class OccurrenceTests
	{
		public OccurrenceTests()
		{
			var serviceProvider = new ServiceCollection().BuildServiceProvider();
			_store = new InMemoryBackgroundTaskStore(serviceProvider, r => DateTimeOffset.Now);
		}

		private readonly IBackgroundTaskStore _store;

		[Fact]
		public void Occurrence_is_in_UTC()
		{
			var task = new BackgroundTask
			{
				RunAt = _store.GetTaskTimestamp(), Expression = CronTemplates.Daily(1, 3, 30)
			};

			var next = task.NextOccurrence;
			Assert.NotNull(next);
			Assert.True(next.Value.Hour == 3);
			Assert.Equal(next.Value.Hour, next.Value.UtcDateTime.Hour);
		}
	}
}