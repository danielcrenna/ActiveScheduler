// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NCrontab;

namespace ActiveScheduler.Models
{
	public static class CronTemplates
	{
		private static readonly CrontabSchedule.ParseOptions ParseOptions =
			new CrontabSchedule.ParseOptions {IncludingSeconds = true};

		public static string Secondly(int seconds = 0)
		{
			return $"{ValueOrStar(seconds)} * * * * *";
		}

		public static string Minutely(int minutes = 0, int atSecond = 0)
		{
			return $"{atSecond} {ValueOrStar(minutes)} * * * *";
		}

		public static string Hourly(int hours = 0, int atMinute = 0, int atSecond = 0)
		{
			return $"{atSecond} {atMinute} {ValueOrStar(hours)} * * *";
		}

		public static string Daily(int days, int atHour = 0, int atMinute = 0, int atSecond = 0)
		{
			return $"{atSecond} {atMinute} {atHour} */{days} * *";
		}

		public static string Weekly(DayOfWeek onDay, int atHour = 0, int atMinute = 0, int atSecond = 0)
		{
			return $"{atSecond} {atMinute} {atHour} * * {(int) onDay}";
		}

		public static string Weekly(int atHour = 0, int atMinute = 0, int atSecond = 0, params DayOfWeek[] onDays)
		{
			if (onDays.Length == 0)
			{
				return null;
			}

			var expression = Weekly(onDays[0], atHour, atMinute, atSecond);

			for (var i = 1; i < onDays.Length; i++)
			{
				expression = $"{expression},{(int) onDays[i]}";
			}

			return expression;
		}

		public static string Monthly(int onDay = 1, int atHour = 0, int atMinute = 0, int atSecond = 0)
		{
			return $"{atSecond} {atMinute} {atHour} {onDay} * *";
		}

		private static string ValueOrStar(int value)
		{
			return value == 0 ? "*" : $"*/{value}";
		}

		public static CrontabSchedule Parse(string expression)
		{
			return CrontabSchedule.Parse(expression, ParseOptions);
		}

		public static bool TryParse(string expression, out CrontabSchedule schedule)
		{
			schedule = CrontabSchedule.TryParse(expression, ParseOptions);
			return schedule != null;
		}
	}
}