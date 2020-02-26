// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using ActiveScheduler.Models;

namespace ActiveScheduler.Hooks
{
	// ReSharper disable once InconsistentNaming
	public interface Before : Hook
	{
		Task BeforeAsync(ExecutionContext context);
	}
}