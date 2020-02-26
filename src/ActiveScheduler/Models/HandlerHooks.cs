// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ActiveScheduler.Hooks;

namespace ActiveScheduler.Models
{
	/// <summary>
	///     Stores method hooks. Used as a cache key for running tasks.
	/// </summary>
	internal class HandlerHooks
	{
		internal Before OnBefore { get; set; }
		internal Handler Handler { get; set; }
		internal After OnAfter { get; set; }
		internal Success OnSuccess { get; set; }
		internal Failure OnFailure { get; set; }
		internal Halt OnHalt { get; set; }
		internal Error OnError { get; set; }
	}
}