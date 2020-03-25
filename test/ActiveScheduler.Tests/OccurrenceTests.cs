using System;
using ActiveScheduler.Models;
using Xunit;

namespace ActiveScheduler.Tests
{
	public class OccurrenceTests
	{
		private readonly IBackgroundTaskStore _store;

		public OccurrenceTests()
		{
			_store = new InMemoryBackgroundTaskStore(()=> DateTimeOffset.Now);
		}

		[Fact]
		public void Occurrence_is_in_UTC()
		{
            var task = new BackgroundTask {
	            RunAt = _store.GetTaskTimestamp(), 
	            Expression = CronTemplates.Daily(1, 3, 30)};

            var next = task.NextOccurrence;
			Assert.NotNull(next);
			Assert.True(next.Value.Hour == 3);
			Assert.Equal(next.Value.Hour, next.Value.UtcDateTime.Hour);
		}
	}
}
