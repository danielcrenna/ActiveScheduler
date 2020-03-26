// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ActiveScheduler.Tests.Internal
{
	public abstract class LoggingScope : ILogger
	{
		#region Logging API

		public void LogDebug(EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Debug, eventId, exception, message, args);
		}

		public void LogDebug(EventId eventId, string message, params object[] args)
		{
			Log(LogLevel.Debug, eventId, message, args);
		}

		public void LogDebug(Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Debug, exception, message, args);
		}

		public void LogDebug(string message, params object[] args)
		{
			Log(LogLevel.Debug, message, args);
		}

		public void LogTrace(EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Trace, eventId, exception, message, args);
		}

		public void LogTrace(EventId eventId, string message, params object[] args)
		{
			Log(LogLevel.Trace, eventId, message, args);
		}

		public void LogTrace(Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Trace, exception, message, args);
		}

		public void LogTrace(string message, params object[] args)
		{
			Log(LogLevel.Trace, message, args);
		}

		public void LogInformation(EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Information, eventId, exception, message, args);
		}

		public void LogInformation(EventId eventId, string message, params object[] args)
		{
			Log(LogLevel.Information, eventId, message, args);
		}

		public void LogInformation(Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Information, exception, message, args);
		}

		public void LogInformation(string message, params object[] args)
		{
			Log(LogLevel.Information, message, args);
		}

		public void LogWarning(EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Warning, eventId, exception, message, args);
		}

		public void LogWarning(EventId eventId, string message, params object[] args)
		{
			Log(LogLevel.Warning, eventId, message, args);
		}

		public void LogWarning(Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Warning, exception, message, args);
		}

		public void LogWarning(string message, params object[] args)
		{
			Log(LogLevel.Warning, message, args);
		}

		public void LogError(EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Error, eventId, exception, message, args);
		}

		public void LogError(EventId eventId, string message, params object[] args)
		{
			Log(LogLevel.Error, eventId, message, args);
		}

		public void LogError(Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Error, exception, message, args);
		}

		public void LogError(string message, params object[] args)
		{
			Log(LogLevel.Error, message, args);
		}

		public void LogCritical(EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Critical, eventId, exception, message, args);
		}

		public void LogCritical(EventId eventId, string message, params object[] args)
		{
			Log(LogLevel.Critical, eventId, message, args);
		}

		public void LogCritical(Exception exception, string message, params object[] args)
		{
			Log(LogLevel.Critical, exception, message, args);
		}

		public void LogCritical(string message, params object[] args)
		{
			Log(LogLevel.Critical, message, args);
		}

		public void Log(LogLevel logLevel, string message, params object[] args)
		{
			Log(logLevel, 0, null, message, args);
		}

		public void Log(LogLevel logLevel, EventId eventId, string message, params object[] args)
		{
			Log(logLevel, eventId, null, message, args);
		}

		public void Log(LogLevel logLevel, Exception exception, string message, params object[] args)
		{
			Log(logLevel, 0, exception, message, args);
		}

		public void Log(LogLevel logLevel, EventId eventId, Exception exception, string message, params object[] args)
		{
			Log(logLevel, eventId, (IReadOnlyList<KeyValuePair<string, object>>) new FormattedLogValues(message, args),
				exception, MessageFormatter);
		}

		public IDisposable BeginScope(string messageFormat, params object[] args)
		{
			return BeginScope<IReadOnlyList<KeyValuePair<string, object>>>(
				new FormattedLogValues(messageFormat, args)
			);
		}

		#endregion

		#region Implementation of ILogger

		protected static string MessageFormatter(object state, Exception error)
		{
			return state.ToString();
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
			Func<TState, Exception, string> formatter)
		{
			var logger = GetLogger();
			logger?.Log(logLevel, eventId, state, exception, formatter);
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			var logger = GetLogger();
			return logger != null && logger.IsEnabled(logLevel);
		}

		public IDisposable BeginScope<TState>(TState state)
		{
			var logger = GetLogger();
			return logger?.BeginScope(state);
		}

		public abstract ILogger GetLogger();

		#endregion
	}
}