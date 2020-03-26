// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading.Tasks;
using ActiveScheduler.Hooks;
using ActiveScheduler.Models;
using Microsoft.Extensions.Logging;

namespace ActiveScheduler.Tests
{
	public class StaticCountingHandler : Handler
	{
		private readonly ILoggerFactory _factory;
		private readonly ILogger<StaticCountingHandler> _logger;

		public StaticCountingHandler(ILogger<StaticCountingHandler> logger, ILoggerFactory factory)
		{
			_logger = logger;
			_factory = factory;
		}

		public static int Count { get; set; }

		public static object Data { get; set; }

		public Task PerformAsync(ExecutionContext context)
		{
			if (!context.TryGetData("Foo", out var data))
				Trace.WriteLine("missing data");
			Data = data;
			Count++;
			return Task.CompletedTask;
		}
	}
}