// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveScheduler.Models;

namespace ActiveScheduler.Internal
{
	internal sealed class LocalServerTimestampService : IServerTimestampService
	{
		public DateTimeOffset GetCurrentTime() => DateTimeOffset.Now;
	}
}