// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ActiveScheduler.Configuration
{
	public class StoreOptions
	{
		public bool CreateIfNotExists { get; set; } = true;
		public bool MigrateOnStartup { get; set; } = true;
		public bool FilterCorrelatedTasks { get; set; } = true;
	}
}