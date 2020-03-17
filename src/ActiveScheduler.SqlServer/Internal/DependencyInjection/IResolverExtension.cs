// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	public interface IResolverExtension
	{
		bool CanResolve(Lifetime lifetime);
		Func<T> Memoize<T>(IDependencyResolver host, Func<T> f);
		Func<IDependencyResolver, T> Memoize<T>(IDependencyResolver host, Func<IDependencyResolver, T> f);
	}
}