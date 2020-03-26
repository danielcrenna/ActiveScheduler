// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace ActiveScheduler.Tests.Internal
{
	public sealed class DelegateLoggerProvider : ILoggerProvider
	{
		private readonly Action<string> _writeLine;

		public DelegateLoggerProvider(Action<string> writeLine) => _writeLine = writeLine;

		public ILogger CreateLogger(string categoryName)
		{
			return new DelegateLogger(categoryName, _writeLine);
		}

		public void Dispose()
		{
		}
	}
}