// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;

namespace ActiveScheduler.Tests.Internal
{
	internal sealed class DelegateTraceListener : TraceListener
	{
		private readonly StringBuilder _buffer;
		private readonly Action<string> _writeLine;

		public DelegateTraceListener(Action<string> writeLine = null)
		{
			_writeLine = writeLine;
			_buffer = new StringBuilder();
		}

		public override void WriteLine(string value)
		{
			try
			{
				Write(value);
				_writeLine?.Invoke(value);
			}
			catch (InvalidOperationException)
			{
				/* do nothing */
			}
			finally
			{
				_buffer.Clear();
			}
		}

		public override void Write(string s)
		{
			_buffer.Append(s);
		}
	}
}