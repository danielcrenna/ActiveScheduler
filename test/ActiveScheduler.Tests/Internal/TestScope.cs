// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace ActiveScheduler.Tests.Internal
{
	public abstract class TestScope : LoggingScope
	{
		protected readonly ILoggerFactory DefaultLoggerFactory = new LoggerFactory();
		public IServiceProvider ServiceProvider;

		protected static DelegateLoggerProvider CreateLoggerProvider()
		{
			var actionLoggerProvider = new DelegateLoggerProvider(message =>
			{
				var outputProvider = AmbientContext.OutputProvider;
				if (outputProvider?.IsAvailable != true)
					return;
				outputProvider.WriteLine(message);
			});
			return actionLoggerProvider;
		}
	}
}