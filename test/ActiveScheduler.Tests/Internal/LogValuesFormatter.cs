// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ActiveScheduler.Tests.Internal
{
	internal sealed class LogValuesFormatter
	{
		private const string NullValue = "(null)";
		private static readonly object[] EmptyArray = new object[0];
		private static readonly char[] FormatDelimiters = {',', ':'};
		private readonly string _format;

		public LogValuesFormatter(string format)
		{
			OriginalFormat = format;

			var sb = new StringBuilder();
			var scanIndex = 0;
			var endIndex = format.Length;

			while (scanIndex < endIndex)
			{
				var openBraceIndex = FindBraceIndex(format, '{', scanIndex, endIndex);
				var closeBraceIndex = FindBraceIndex(format, '}', openBraceIndex, endIndex);

				if (closeBraceIndex == endIndex)
				{
					sb.Append(format, scanIndex, endIndex - scanIndex);
					scanIndex = endIndex;
				}
				else
				{
					// Format item syntax : { index[,alignment][ :formatString] }.
					var formatDelimiterIndex =
						FindIndexOfAny(format, FormatDelimiters, openBraceIndex, closeBraceIndex);

					sb.Append(format, scanIndex, openBraceIndex - scanIndex + 1);
					sb.Append(ValueNames.Count.ToString(CultureInfo.InvariantCulture));
					ValueNames.Add(format.Substring(openBraceIndex + 1, formatDelimiterIndex - openBraceIndex - 1));
					sb.Append(format, formatDelimiterIndex, closeBraceIndex - formatDelimiterIndex + 1);

					scanIndex = closeBraceIndex + 1;
				}
			}

			_format = sb.ToString();
		}

		public string OriginalFormat { get; }
		public List<string> ValueNames { get; } = new List<string>();

		private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
		{
			// Example: {{prefix{{{Argument}}}suffix}}.
			var braceIndex = endIndex;
			var scanIndex = startIndex;
			var braceOccurenceCount = 0;

			while (scanIndex < endIndex)
			{
				if (braceOccurenceCount > 0 && format[scanIndex] != brace)
				{
					if (braceOccurenceCount % 2 == 0)
					{
						// Even number of '{' or '}' found. Proceed search with next occurence of '{' or '}'.
						braceOccurenceCount = 0;
						braceIndex = endIndex;
					}
					else
					{
						// An unescaped '{' or '}' found.
						break;
					}
				}
				else if (format[scanIndex] == brace)
				{
					if (brace == '}')
					{
						if (braceOccurenceCount == 0)
						{
							// For '}' pick the first occurence.
							braceIndex = scanIndex;
						}
					}
					else
					{
						// For '{' pick the last occurence.
						braceIndex = scanIndex;
					}

					braceOccurenceCount++;
				}

				scanIndex++;
			}

			return braceIndex;
		}

		private static int FindIndexOfAny(string format, char[] chars, int startIndex, int endIndex)
		{
			var findIndex = format.IndexOfAny(chars, startIndex, endIndex - startIndex);
			return findIndex == -1 ? endIndex : findIndex;
		}

		public string Format(object[] values)
		{
			if (values == null)
				return string.Format(CultureInfo.InvariantCulture, _format, EmptyArray);

			for (var i = 0; i < values.Length; i++)
				values[i] = FormatArgument(values[i]);

			return string.Format(CultureInfo.InvariantCulture, _format, values);
		}

		internal string Format()
		{
			return _format;
		}

		internal string Format(object arg0)
		{
			return string.Format(CultureInfo.InvariantCulture, _format, FormatArgument(arg0));
		}

		internal string Format(object arg0, object arg1)
		{
			return string.Format(CultureInfo.InvariantCulture, _format, FormatArgument(arg0), FormatArgument(arg1));
		}

		internal string Format(object arg0, object arg1, object arg2)
		{
			return string.Format(CultureInfo.InvariantCulture, _format, FormatArgument(arg0), FormatArgument(arg1),
				FormatArgument(arg2));
		}

		public KeyValuePair<string, object> GetValue(object[] values, int index)
		{
			return index < 0 || index > ValueNames.Count ? throw new IndexOutOfRangeException(nameof(index)) :
				ValueNames.Count > index ? new KeyValuePair<string, object>(ValueNames[index], values[index]) :
				new KeyValuePair<string, object>("{OriginalFormat}", OriginalFormat);
		}

		public IEnumerable<KeyValuePair<string, object>> GetValues(object[] values)
		{
			var valueArray = new KeyValuePair<string, object>[values.Length + 1];
			for (var index = 0; index != ValueNames.Count; ++index)
			{
				valueArray[index] = new KeyValuePair<string, object>(ValueNames[index], values[index]);
			}

			valueArray[valueArray.Length - 1] = new KeyValuePair<string, object>("{OriginalFormat}", OriginalFormat);
			return valueArray;
		}

		private static object FormatArgument(object value)
		{
			// since 'string' implements IEnumerable, special case it
			// if the value implements IEnumerable, build a comma-separated string.
			return value == null
				? NullValue
				: value is string
					? value
					: value is IEnumerable enumerable
						? string.Join(", ", enumerable.Cast<object>().Select(o => o ?? NullValue))
						: value;
		}
	}
}