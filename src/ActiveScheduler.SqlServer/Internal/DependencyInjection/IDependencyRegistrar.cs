// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	public interface IDependencyRegistrar : IDisposable
	{
		IDependencyRegistrar Register(Type type, Func<object> builder, Lifetime lifetime = Lifetime.AlwaysNew);
		IDependencyRegistrar Register<T>(Func<T> builder, Lifetime lifetime = Lifetime.AlwaysNew) where T : class;

		IDependencyRegistrar Register<T>(string name, Func<T> builder, Lifetime lifetime = Lifetime.AlwaysNew)
			where T : class;

		IDependencyRegistrar Register<T>(Func<IDependencyResolver, T> builder, Lifetime lifetime = Lifetime.AlwaysNew)
			where T : class;

		IDependencyRegistrar Register<T>(string name, Func<IDependencyResolver, T> builder,
			Lifetime lifetime = Lifetime.AlwaysNew) where T : class;

		IDependencyRegistrar Register<T>(T instance);
		IDependencyRegistrar Register(object instance);
		bool TryRegister<T>(T instance);
	}
}