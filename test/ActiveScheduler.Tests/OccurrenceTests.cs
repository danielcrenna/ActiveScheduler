using System;
using ActiveScheduler.Models;

namespace ActiveScheduler.Tests
{
	public class OccurrenceTests
	{
		public bool Occurrence_is_in_UTC()
		{
            var task = new BackgroundTask
            {
	            RunAt = DateTimeOffset.UtcNow, 
	            Expression = CronTemplates.Daily(1, 3, 30)
            };
			var next = task.NextOccurrence;

			return next != null && 
			       next.Value.Hour == 3 && 
			       next.Value.Hour.Equals(next.Value.UtcDateTime.Hour);
		}
	}
}
