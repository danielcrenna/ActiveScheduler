// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler.Configuration
{
	public static class IntervalFunctionExtensions
	{
		public static readonly Func<int, TimeSpan> ExponentialBackoff = i => TimeSpan.FromSeconds(5 + Math.Pow(i, 4));

		public static TimeSpan NextInterval(this IntervalFunction function, int attempts)
		{
			return function switch
			{
				IntervalFunction.ExponentialBackoff => ExponentialBackoff(attempts),
				_ => throw new ArgumentOutOfRangeException(nameof(function), function, null)
			};
		}
	}
}