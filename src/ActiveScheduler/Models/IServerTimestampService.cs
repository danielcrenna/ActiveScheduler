// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler.Models
{
	public interface IServerTimestampService
	{
		/// <summary>
		///     Retrieves the current instantaneous time, with time zone offset. This should only be used for operator-level
		///     activities that are not displayed to a user, such as transaction logs.
		/// </summary>
		DateTimeOffset GetCurrentTime();
	}
}