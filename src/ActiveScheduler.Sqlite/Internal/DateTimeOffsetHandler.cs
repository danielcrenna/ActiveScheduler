// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using Dapper;

namespace ActiveScheduler.Sqlite.Internal
{
	internal sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset?>
	{
		public static readonly DateTimeOffsetHandler Default = new DateTimeOffsetHandler();

		public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
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

		public override DateTimeOffset? Parse(object value)
		{
			switch (value)
			{
				case null:
					return null;
				case DateTimeOffset offset:
					return offset;
				default:
					return Convert.ToDateTime(value);
			}
		}
	}
}