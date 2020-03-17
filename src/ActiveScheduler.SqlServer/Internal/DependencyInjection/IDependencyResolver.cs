// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	public interface IDependencyResolver : IDisposable
	{
		T Resolve<T>() where T : class;
		T MustResolve<T>() where T : class;

		object Resolve(Type serviceType);
		object MustResolve(Type serviceType);

		T Resolve<T>(string name) where T : class;
		T MustResolve<T>(string name) where T : class;

		object Resolve(string name, Type serviceType);
		object MustResolve(string name, Type serviceType);

		IEnumerable<T> ResolveAll<T>() where T : class;
		IEnumerable ResolveAll(Type serviceType);
	}
}