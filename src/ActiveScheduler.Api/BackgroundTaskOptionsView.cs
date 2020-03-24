// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using ActiveScheduler.Configuration;

namespace ActiveScheduler.Api
{
	public class BackgroundTaskOptionsView
	{
		public BackgroundTaskOptions Options { get; set; }
		public IEnumerable<string> TaskTypes { get; set; }
	}
}