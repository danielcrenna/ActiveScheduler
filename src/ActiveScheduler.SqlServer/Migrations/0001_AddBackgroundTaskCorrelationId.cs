// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentMigrator;

namespace ActiveScheduler.SqlServer.Migrations
{
	[Migration(1)]
	public class AddBackgroundTaskCorrelationId : Migration
	{
		public override void Up()
		{
			Alter.Table(nameof(BackgroundTask))
				.AddColumn(nameof(BackgroundTask.CorrelationId)).AsGuid().NotNullable()
				;
		}

		public override void Down()
		{
			Delete.Column(nameof(BackgroundTask.CorrelationId)).FromTable(nameof(BackgroundTask));
		}
	}
}