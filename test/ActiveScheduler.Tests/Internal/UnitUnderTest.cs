// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace ActiveScheduler.Tests.Internal
{
	public class UnitUnderTest : LoggingScope
	{
		protected readonly ILoggerFactory DefaultLoggerFactory = new LoggerFactory();

		public override ILogger GetLogger()
		{
			return DefaultLoggerFactory.CreateLogger<UnitUnderTest>();
		}
	}
}