// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler
{
	public static class BackgroundTaskExtensions
	{
		public static void RepeatIndefinitely(this BackgroundTask task, string expression)
		{
			task.Expression = expression;
		}

		public static void RepeatUntil(this BackgroundTask task, string expression, DateTimeOffset end)
		{
			task.Expression = expression;
			task.End = end;
		}
	}
}