// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using ActiveScheduler.Hooks;
using ActiveScheduler.Models;

namespace ActiveScheduler.Tests
{
	public class TerminalTaskHandler : Handler
	{
		public static int Count { get; set; }

		public async Task PerformAsync(ExecutionContext context)
		{
			await Task.Delay(TimeSpan.FromSeconds(1000));
		}
	}
}