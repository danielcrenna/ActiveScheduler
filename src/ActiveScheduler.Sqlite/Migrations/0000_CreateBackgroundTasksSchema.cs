// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentMigrator;

namespace ActiveScheduler.Sqlite.Migrations
{
	[Migration(0)]
	public class CreateBackgroundTasksSchema : Migration
	{
		public override void Up()
		{
			Create.Table(nameof(BackgroundTask))
				.WithColumn("Id").AsInt32().PrimaryKey().Identity()
				.WithColumn("Priority").AsInt32().NotNullable().WithDefaultValue(0)
				.WithColumn("Attempts").AsInt32().NotNullable().WithDefaultValue(0)
				.WithColumn("Handler").AsString(int.MaxValue).NotNullable()
				.WithColumn("RunAt").AsDateTime().NotNullable()
				.WithColumn("MaximumRuntime").AsTime().NotNullable()
				.WithColumn("MaximumAttempts").AsInt32().NotNullable()
				.WithColumn("DeleteOnSuccess").AsBoolean().NotNullable()
				.WithColumn("DeleteOnFailure").AsBoolean().NotNullable()
				.WithColumn("DeleteOnError").AsBoolean().NotNullable()
				.WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
				.WithColumn("LastError").AsString().Nullable()
				.WithColumn("FailedAt").AsDateTime().Nullable()
				.WithColumn("SucceededAt").AsDateTime().Nullable()
				.WithColumn("LockedAt").AsDateTime().Nullable()
				.WithColumn("LockedBy").AsString().Nullable()
				.WithColumn("Expression").AsAnsiString().Nullable()
				.WithColumn("Start").AsDateTime().NotNullable()
				.WithColumn("ContinueOnSuccess").AsBoolean().NotNullable()
				.WithColumn("ContinueOnFailure").AsBoolean().NotNullable()
				.WithColumn("ContinueOnError").AsBoolean().NotNullable()
				.WithColumn("End").AsDateTime().Nullable()
				;
			Create.Table($"{nameof(BackgroundTask)}_Tag")
				.WithColumn("Id").AsInt32().PrimaryKey().Identity()
				.Unique() // CLUSTERED INDEX + UNIQUE (Faster Lookups)
				.WithColumn("Name").AsString().NotNullable().Unique()
				;
			Create.Table($"{nameof(BackgroundTask)}_Tags")
				.WithColumn("Id").AsInt32().PrimaryKey().Identity()
				.Unique() // CLUSTERED INDEX + UNIQUE (Faster Lookups)
				.WithColumn($"{nameof(BackgroundTask)}Id").AsInt32().ForeignKey($"{nameof(BackgroundTask)}", "Id")
				.NotNullable().Indexed()
				.WithColumn("TagId").AsInt32().ForeignKey($"{nameof(BackgroundTask)}_Tag", "Id").NotNullable().Indexed()
				;
		}

		public override void Down()
		{
			Delete.Table($"{nameof(BackgroundTask)}_Tags");
			Delete.Table($"{nameof(BackgroundTask)}_Tag");
			Delete.Table($"{nameof(BackgroundTask)}");
		}
	}
}