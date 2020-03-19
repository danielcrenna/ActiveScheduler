// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Data;
using ActiveResolver;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public static class Add
	{
		private static readonly ConcurrentDictionary<string, DependencyContainer> Containers = new ConcurrentDictionary<string, DependencyContainer>();
		
		public static IServiceCollection AddDatabaseConnection<TScope, TConnectionFactory>(this IServiceCollection services, string connectionString, ConnectionScope scope = ConnectionScope.AlwaysNew) where TConnectionFactory : class, IConnectionFactory, new()
		{
			var slot = $"{typeof(TScope).FullName}";

			var factory = new TConnectionFactory {ConnectionString = connectionString};
			services.AddSingleton(factory);

			var container = services.AddOrGetContainer(slot);
			container.Register(slot, factory);

			switch (scope)
			{
				case ConnectionScope.AlwaysNew:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot)));
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot)));
					break;
				case ConnectionScope.ByRequest:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot)),
						b => WrapLifecycle(container, b, Lifetime.Request));
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot)),
						b => WrapLifecycle(container, b, Lifetime.Request));
					break;
				case ConnectionScope.ByThread:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot)),
						b => WrapLifecycle(container, b, Lifetime.Thread));
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot)),
						b => WrapLifecycle(container, b, Lifetime.Thread));
					break;
				case ConnectionScope.KeepAlive:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot)),
						b => WrapLifecycle(container, b, Lifetime.Permanent));
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot)), 
						b => WrapLifecycle(container, b, Lifetime.Permanent));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
			}
			
			if(!TryGetContainer(slot, out container))
				throw new ArgumentException($"Could not initialize container with slot {slot}", slot);

			services.AddTransient(r => container.Resolve<IDataConnection<TScope>>());

			switch (scope)
			{
				case ConnectionScope.AlwaysNew:
					container.Register<IDataConnection<TScope>>(r =>
						new DataConnection<TScope>(r.Resolve<DataContext>(slot)));
					break;
				case ConnectionScope.ByRequest:
					container.Register<IDataConnection<TScope>>(
						r => new DataConnection<TScope>(r.Resolve<DataContext>(slot)), b => WrapLifecycle(container, b, Lifetime.Request));
					break;
				case ConnectionScope.ByThread:
					container.Register<IDataConnection<TScope>>(
						r => new DataConnection<TScope>(r.Resolve<DataContext>(slot)), b => WrapLifecycle(container, b, Lifetime.Thread));
					break;
				case ConnectionScope.KeepAlive:
					container.Register<IDataConnection<TScope>>(
						r => new DataConnection<TScope>(r.Resolve<DataContext>(slot)), b => WrapLifecycle(container, b, Lifetime.Permanent));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
			}

			return services;
		}

		private static DependencyContainer AddOrGetContainer(this IServiceCollection services, string slot)
		{
			if (Containers.TryGetValue(slot, out var container))
				return container;

			var serviceProvider = services.BuildServiceProvider();
			container = new DependencyContainer(serviceProvider);
			Containers.TryAdd(slot, container);

			return container;
		}

		private static bool TryGetContainer(string slot, out DependencyContainer container)
		{
			return Containers.TryGetValue(slot, out container);
		}

		private static Func<DependencyContainer, T> WrapLifecycle<T>(DependencyContainer host, Func<DependencyContainer, T> builder, Lifetime lifetime)
			where T : class
		{
			var registration = lifetime switch
			{
				Lifetime.AlwaysNew => builder,
				Lifetime.Permanent => InstanceIsUnique.PerProcess(builder),
				Lifetime.Thread => InstanceIsUnique.PerThread(host, builder),
				Lifetime.Request => InstanceIsUnique.PerHttpRequest(builder),
				_ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "No extensions can serve this lifetime.")
			};

			return registration;
		}

		public enum Lifetime
		{
			AlwaysNew,
			Permanent,
			Thread,
			Request
		}
	}
}