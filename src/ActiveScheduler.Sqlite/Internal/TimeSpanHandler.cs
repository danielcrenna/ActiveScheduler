// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using Dapper;

namespace ActiveScheduler.Sqlite.Internal
{
	internal sealed class TimeSpanHandler : SqlMapper.TypeHandler<TimeSpan?>
	{
		public static readonly TimeSpanHandler Default = new TimeSpanHandler();

		public override void SetValue(IDbDataParameter parameter, TimeSpan? value)
		{
			if (value.HasValue)
			{
				parameter.Value = value.Value;
			}
			else
			{
				parameter.Value = DBNull.Value;
			}
		}

		public override TimeSpan? Parse(object value)
		{
			switch (value)
			{
				case null:
					return null;
				case TimeSpan timeSpan:
					return timeSpan;
				default:
					return TimeSpan.Parse(value.ToString());
			}
		}
	}
}