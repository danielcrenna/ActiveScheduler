// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ActiveScheduler.Tests.Internal
{
	public class XunitLoggerProvider : ILoggerProvider
	{
		private readonly ITestOutputHelper _helper;

		public XunitLoggerProvider(ITestOutputHelper helper) => _helper = helper;

		public void Dispose() { }

		public ILogger CreateLogger(string categoryName)
		{
			return new XunitLogger(_helper);
		}
	}
}