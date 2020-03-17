// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentMigrator;

namespace ActiveScheduler.SqlServer.Migrations
{
	[Migration(0)]
	public class CreateBackgroundTasksSchema : Migration
	{
		public override void Up()
		{
			const string schema = "dbo";

			Execute.Sql($"CREATE SEQUENCE [{schema}].[{nameof(BackgroundTask)}_Id] START WITH 1 INCREMENT BY 1");

			Create.Table(nameof(BackgroundTask))
				.WithColumn("Id")
				.AsCustom($"INT DEFAULT(NEXT VALUE FOR [{schema}].[{nameof(BackgroundTask)}_Id]) PRIMARY KEY CLUSTERED")
				.WithColumn("Priority").AsInt32().NotNullable().WithDefaultValue(0)
				.WithColumn("Attempts").AsInt32().NotNullable().WithDefaultValue(0)
				.WithColumn("Handler").AsString(int.MaxValue).NotNullable()
				.WithColumn("RunAt").AsDateTimeOffset().NotNullable()
				.WithColumn("MaximumRuntime").AsTime().NotNullable()
				.WithColumn("MaximumAttempts").AsInt32().NotNullable()
				.WithColumn("DeleteOnSuccess").AsBoolean().NotNullable()
				.WithColumn("DeleteOnFailure").AsBoolean().NotNullable()
				.WithColumn("DeleteOnError").AsBoolean().NotNullable()
				.WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
				.WithColumn("LastError").AsString().Nullable()
				.WithColumn("FailedAt").AsDateTimeOffset().Nullable()
				.WithColumn("SucceededAt").AsDateTimeOffset().Nullable()
				.WithColumn("LockedAt").AsDateTimeOffset().Nullable()
				.WithColumn("LockedBy").AsString().Nullable()
				.WithColumn("Expression").AsAnsiString().Nullable()
				.WithColumn("Start").AsDateTimeOffset().NotNullable()
				.WithColumn("ContinueOnSuccess").AsBoolean().NotNullable()
				.WithColumn("ContinueOnFailure").AsBoolean().NotNullable()
				.WithColumn("ContinueOnError").AsBoolean().NotNullable()
				.WithColumn("End").AsDateTimeOffset().Nullable()
				.WithColumn("Data").AsString(int.MaxValue).Nullable()
				;
			Create.Table($"{nameof(BackgroundTask)}_Tag")
				.WithColumn("Id").AsInt32().PrimaryKey().Identity()
				.Unique() // CLUSTERED INDEX + UNIQUE (Faster Lookups)
				.WithColumn("Name").AsString().NotNullable().Indexed() // ORDER BY ScheduledTask_Tag.Name ASC
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
			Execute.Sql($"DROP TABLE [{nameof(BackgroundTask)}_Tags]");
			Execute.Sql($"DROP TABLE [{nameof(BackgroundTask)}_Tag]");
			Execute.Sql($"DROP TABLE [{nameof(BackgroundTask)}]");
			Execute.Sql($"DROP SEQUENCE [{nameof(BackgroundTask)}_Id]");
		}
	}
}