// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using ActiveLogging;
using ActiveScheduler.Hooks;
using ActiveScheduler.Models;

namespace ActiveScheduler.Api
{
	[Description("A test function for diagnostic purposes.")]
	public sealed class TestFunction : Before, Handler, After, Success, Failure, Halt, Error
	{
		private readonly ISafeLogger<TestFunction> _logger;

		public TestFunction(ISafeLogger<TestFunction> logger) => _logger = logger;

		public Task AfterAsync(ExecutionContext context)
		{
			_logger.Debug(() => $"{nameof(After)} executed.");
			return Task.CompletedTask;
		}

		public Task BeforeAsync(ExecutionContext context)
		{
			_logger.Debug(() => $"{nameof(Before)} executed.");
			return Task.CompletedTask;
		}

		public Task ErrorAsync(ExecutionContext context, Exception error)
		{
			_logger.Debug(() => $"{nameof(Error)} executed with error {error.Message}");
			return Task.CompletedTask;
		}

		public Task FailureAsync(ExecutionContext context)
		{
			_logger.Debug(() => $"{nameof(Failure)} executed.");
			return Task.CompletedTask;
		}

		public Task HaltAsync(ExecutionContext context, bool immediate)
		{
			_logger.Debug(() => $"{nameof(Halt)} executed{(immediate ? " immediately" : "")}.");
			return Task.CompletedTask;
		}

		public Task PerformAsync(ExecutionContext context)
		{
			_logger.Debug(() => $"{nameof(PerformAsync)} executed.");
			if (context.TryGetData("Success", out var succeed) && succeed is bool flag && flag)
				context.Succeed();
			else
				context.Fail();
			return Task.CompletedTask;
		}

		public Task SuccessAsync(ExecutionContext context)
		{
			_logger.Debug(() => $"{nameof(SuccessAsync)} executed.");
			return Task.CompletedTask;
		}
	}
}