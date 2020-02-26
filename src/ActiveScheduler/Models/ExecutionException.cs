// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler.Models
{
	public class ExecutionException : Exception
	{
		public ExecutionException(ExecutionContext context, string message) : base(message) => Context = context;

		public ExecutionContext Context { get; set; }
	}
}