// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentMigrator;

namespace ActiveScheduler.Sqlite.Migrations
{
	[Migration(1)]
	public class AddBackgroundTaskCorrelationId : Migration
	{
		public override void Up()
		{
			Alter.Table(nameof(BackgroundTask))
				.AddColumn(nameof(BackgroundTask.CorrelationId)).AsGuid()
				.Nullable() /* Can't add a non-default column in SQLite, even if no data exists */
				;
		}

		public override void Down()
		{
			Delete.Column(nameof(BackgroundTask.CorrelationId)).FromTable(nameof(BackgroundTask));
		}
	}
}