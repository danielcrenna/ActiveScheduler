// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ActiveScheduler.Configuration
{
	public enum IntervalFunction
	{
		/// <summary>
		///     5 seconds + N to the fourth power, where N is the number of retries (i.e. exponential back-off)
		/// </summary>
		ExponentialBackoff
	}
}