// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ActiveScheduler.SqlServer.Internal.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public static class Add
	{
		private static readonly ConcurrentDictionary<string, IContainer> Containers =
			new ConcurrentDictionary<string, IContainer>();

		public static ContainerBuilder AddDatabaseConnection<TScope, TConnectionFactory>(
			this IServiceCollection services, string connectionString,
			ConnectionScope scope = ConnectionScope.AlwaysNew,
			IEnumerable<IResolverExtension> extensions = null,
			Action<IDbConnection, IServiceProvider> onConnection = null,
			Action<IDbCommand, Type, IServiceProvider> onCommand = null)
			where TConnectionFactory : class, IConnectionFactory, new()
		{
			var slot = $"{typeof(TScope).FullName}";
			var builder = AddDatabaseConnection<TConnectionFactory>(services, connectionString, scope, slot, extensions,
				onConnection, onCommand);

			if (!TryGetContainer(slot, out var container))
				throw new ArgumentException($"Could not initialize container with slot {slot}", slot);

			services.AddTransient(r => container.Resolve<IDataConnection<TScope>>());

			switch (scope)
			{
				case ConnectionScope.AlwaysNew:
					container.Register<IDataConnection<TScope>>(r =>
						new DataConnection<TScope>(r.Resolve<DataContext>(slot), r.MustResolve<IServiceProvider>(),
							onCommand));
					break;
				case ConnectionScope.ByRequest:
					container.Register<IDataConnection<TScope>>(
						r => new DataConnection<TScope>(r.Resolve<DataContext>(slot), r.MustResolve<IServiceProvider>(),
							onCommand), Lifetime.Request);
					break;
				case ConnectionScope.ByThread:
					container.Register<IDataConnection<TScope>>(
						r => new DataConnection<TScope>(r.Resolve<DataContext>(slot), r.MustResolve<IServiceProvider>(),
							onCommand), Lifetime.Thread);
					break;
				case ConnectionScope.KeepAlive:
					container.Register<IDataConnection<TScope>>(
						r => new DataConnection<TScope>(r.Resolve<DataContext>(slot), r.MustResolve<IServiceProvider>(),
							onCommand), Lifetime.Permanent);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
			}

			return builder;
		}

		private static IContainer AddOrGetContainer(this IServiceCollection services, string slot,
			IEnumerable<IResolverExtension> extensions)
		{
			if (Containers.TryGetValue(slot, out var container))
				return container;

			var serviceProvider = services.BuildServiceProvider();
			container = new DependencyContainer(serviceProvider);
			container.Register(r => serviceProvider);
			foreach (var extension in extensions ?? Enumerable.Empty<IResolverExtension>())
				container.AddExtension(extension);

			Containers.TryAdd(slot, container);

			return container;
		}

		private static bool TryGetContainer(string slot, out IContainer container)
		{
			return Containers.TryGetValue(slot, out container);
		}

		private static ContainerBuilder AddDatabaseConnection<TConnectionFactory>(this IServiceCollection services,
			string connectionString,
			ConnectionScope scope,
			string slot,
			IEnumerable<IResolverExtension> extensions = null,
			Action<IDbConnection, IServiceProvider> onConnection = null,
			Action<IDbCommand, Type, IServiceProvider> onCommand = null)
			where TConnectionFactory : class, IConnectionFactory, new()
		{
			var factory = new TConnectionFactory {ConnectionString = connectionString};
			services.AddSingleton(factory);

			var container = services.AddOrGetContainer(slot, extensions);
			container.Register(slot, r => factory, Lifetime.Permanent);

			switch (scope)
			{
				case ConnectionScope.AlwaysNew:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot), r.Resolve<IServiceProvider>(),
							onConnection));
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot), r.Resolve<IServiceProvider>(),
							onCommand));
					break;
				case ConnectionScope.ByRequest:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot), r.Resolve<IServiceProvider>(),
							onConnection),
						Lifetime.Request);
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot), r.Resolve<IServiceProvider>(), onCommand),
						Lifetime.Request);
					break;
				case ConnectionScope.ByThread:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot), r.Resolve<IServiceProvider>(),
							onConnection),
						Lifetime.Thread);
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot), r.Resolve<IServiceProvider>(), onCommand),
						Lifetime.Thread);
					break;
				case ConnectionScope.KeepAlive:
					container.Register(slot,
						r => new DataContext(r.Resolve<TConnectionFactory>(slot), r.Resolve<IServiceProvider>(),
							onConnection),
						Lifetime.Permanent);
					container.Register<IDataConnection>(slot,
						r => new DataConnection(r.Resolve<DataContext>(slot), r.Resolve<IServiceProvider>(), onCommand),
						Lifetime.Permanent);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
			}

			return new ContainerBuilder(services);
		}
	}
}