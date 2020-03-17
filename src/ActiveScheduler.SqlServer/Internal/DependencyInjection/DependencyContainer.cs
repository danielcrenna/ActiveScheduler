// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using TypeKitchen.Creation;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	internal sealed class DependencyContainer : IContainer
	{
		private readonly List<IResolverExtension> _extensions;
		private readonly IEnumerable<Assembly> _fallbackAssemblies;
		private readonly IServiceProvider _fallbackProvider;

		public DependencyContainer(
			IServiceProvider fallbackProvider = null,
			IEnumerable<Assembly> fallbackAssemblies = null)
		{
			_extensions = new List<IResolverExtension>();

			_fallbackProvider = fallbackProvider;
			_fallbackAssemblies = fallbackAssemblies ?? Enumerable.Empty<Assembly>();
		}

		public bool ThrowIfCantResolve { get; set; }

		public void Dispose() { }

		public bool AddExtension<T>(T extension) where T : IResolverExtension
		{
			lock (_extensions)
			{
				if (_extensions.Contains(extension))
					return false;
				_extensions.Add(extension);
				return true;
			}
		}

		#region Register

		private readonly ConcurrentDictionary<Type, Func<object>>
			_registrations = new ConcurrentDictionary<Type, Func<object>>();

		private readonly ConcurrentDictionary<NameAndType, Func<object>> _namedRegistrations =
			new ConcurrentDictionary<NameAndType, Func<object>>();

		private readonly ConcurrentDictionary<Type, List<Func<object>>> _collectionRegistrations =
			new ConcurrentDictionary<Type, List<Func<object>>>();

		public IDependencyRegistrar Register(Type type, Func<object> builder, Lifetime lifetime = Lifetime.AlwaysNew)
		{
			var next = WrapLifecycle(builder, lifetime);
			if (_registrations.ContainsKey(type))
			{
				var previous = _registrations[type];
				_registrations[type] = next;
				RegisterManyUnnamed(type, previous);
			}
			else
			{
				_registrations[type] = next;
			}

			return this;
		}

		public IDependencyRegistrar Register<T>(Func<T> builder, Lifetime lifetime = Lifetime.AlwaysNew) where T : class
		{
			var type = typeof(T);
			Func<object> next = WrapLifecycle(builder, lifetime);
			if (_registrations.ContainsKey(type))
			{
				var previous = _registrations[type];
				_registrations[type] = next;
				RegisterManyUnnamed(type, previous);
			}
			else
			{
				_registrations[type] = next;
			}

			return this;
		}

		public IDependencyRegistrar Register<T>(string name, Func<T> builder, Lifetime lifetime = Lifetime.AlwaysNew)
			where T : class
		{
			var type = typeof(T);
			_namedRegistrations[new NameAndType(name, type)] = WrapLifecycle(builder, lifetime);
			return this;
		}

		public IDependencyRegistrar Register<T>(string name, Func<IDependencyResolver, T> builder,
			Lifetime lifetime = Lifetime.AlwaysNew) where T : class
		{
			var registration = WrapLifecycle(builder, lifetime);
			_namedRegistrations[new NameAndType(name, typeof(T))] = () => registration(this);
			return this;
		}

		public IDependencyRegistrar Register<T>(Func<IDependencyResolver, T> builder,
			Lifetime lifetime = Lifetime.AlwaysNew)
			where T : class
		{
			var type = typeof(T);
			if (_registrations.ContainsKey(type))
			{
				var previous = _registrations[type];
				_registrations[type] = () => WrapLifecycle(builder, lifetime)(this);
				RegisterManyUnnamed(type, previous);
			}
			else
			{
				_registrations[type] = () => WrapLifecycle(builder, lifetime)(this);
			}

			return this;
		}

		public IDependencyRegistrar Register<T>(T instance)
		{
			return Register(typeof(T), instance);
		}

		public IDependencyRegistrar Register(object instance)
		{
			var type = instance.GetType();
			return Register(type, instance);
		}

		private IDependencyRegistrar Register(Type type, object instance)
		{
			if (_registrations.ContainsKey(type))
			{
				var previous = _registrations[type];
				_registrations[type] = () => instance;
				RegisterManyUnnamed(type, previous);
			}
			else
			{
				_registrations[type] = () => instance;
			}

			return this;
		}

		public bool TryRegister<T>(T instance)
		{
			var type = typeof(T);
			if (_registrations.ContainsKey(type))
				return false;
			_registrations[type] = () => instance;
			return true;
		}

		public bool TryRegister(object instance)
		{
			var type = instance.GetType();
			if (_registrations.ContainsKey(type))
				return false;
			_registrations[type] = () => instance;
			return true;
		}

		private void RegisterManyUnnamed(Type type, Func<object> previous)
		{
			if (!_collectionRegistrations.TryGetValue(type, out var collectionBuilder))
			{
				collectionBuilder = new List<Func<object>> {previous};
				_collectionRegistrations.TryAdd(type, collectionBuilder);
			}

			collectionBuilder.Add(_registrations[type]);

			// implied registration of the enumerable equivalent
			Register(typeof(IEnumerable<>).MakeGenericType(type), () =>
			{
				var collection = (IList) Instancing.CreateInstance(typeof(List<>).MakeGenericType(type));
				foreach (var item in YieldCollection(collectionBuilder))
					collection.Add(item);
				return collection;
			}, Lifetime.Permanent);
		}

		#endregion

		#region Resolve

		public T Resolve<T>() where T : class
		{
			return ResolveTyped<T>(ThrowIfCantResolve);
		}

		public T MustResolve<T>() where T : class
		{
			return ResolveTyped<T>(true);
		}

		private T ResolveTyped<T>(bool throwIfCantResolve) where T : class
		{
			var serviceType = typeof(T);
			if (!_registrations.TryGetValue(serviceType, out var builder))
				return AutoResolve(serviceType, throwIfCantResolve) as T;
			return TryTypedResolve<T>(throwIfCantResolve, builder);
		}

		public object Resolve(Type serviceType)
		{
			return ResolveUntyped(serviceType, ThrowIfCantResolve);
		}

		public object MustResolve(Type serviceType)
		{
			return ResolveUntyped(serviceType, true);
		}

		private object ResolveUntyped(Type serviceType, bool throwIfCantResolve)
		{
			return !_registrations.TryGetValue(serviceType, out var builder)
				? AutoResolve(serviceType, throwIfCantResolve)
				: TryUntypedResolve(serviceType, throwIfCantResolve, builder);
		}

		public T Resolve<T>(string name) where T : class
		{
			return ResolveNamedTyped<T>(name, ThrowIfCantResolve);
		}

		public T MustResolve<T>(string name) where T : class
		{
			return ResolveNamedTyped<T>(name, true);
		}

		private T ResolveNamedTyped<T>(string name, bool throwIfCantResolve) where T : class
		{
			if (_namedRegistrations.TryGetValue(new NameAndType(name, typeof(T)), out var builder))
				return TryTypedResolve<T>(throwIfCantResolve, builder);
			if (throwIfCantResolve)
				throw new InvalidOperationException($"No registration for {typeof(T)} named {name}");
			return null;
		}

		public object Resolve(string name, Type serviceType)
		{
			return ResolveNamedUntyped(name, serviceType, ThrowIfCantResolve);
		}

		public object MustResolve(string name, Type serviceType)
		{
			return ResolveNamedUntyped(name, serviceType, true);
		}

		private object ResolveNamedUntyped(string name, Type serviceType, bool throwIfCantResolve)
		{
			if (_namedRegistrations.TryGetValue(new NameAndType(name, serviceType), out var builder))
				return TryUntypedResolve(serviceType, throwIfCantResolve, builder);
			if (throwIfCantResolve)
				throw new InvalidOperationException($"No registration for {serviceType} named {name}");
			return null;
		}

		private static T TryTypedResolve<T>(bool throwIfCantResolve, Func<object> builder) where T : class
		{
			switch (builder())
			{
				case T resolved:
					return resolved;
				case null when throwIfCantResolve:
					throw new InvalidOperationException($"No registration for {typeof(T)}");
				default:
					return null;
			}
		}

		private static object TryUntypedResolve(Type serviceType, bool throwIfCantResolve, Func<object> builder)
		{
			return builder() switch
			{
				{ } resolved => resolved,
				null when throwIfCantResolve => throw new InvalidOperationException(
					$"No registration for {serviceType}"),
				_ => null
			};
		}

		public IEnumerable<T> ResolveAll<T>() where T : class
		{
			var serviceType = typeof(T);
			return _collectionRegistrations.TryGetValue(serviceType, out var collectionBuilder)
				? YieldCollection<T>(collectionBuilder)
				: Enumerable.Empty<T>();
		}

		private static IEnumerable<T> YieldCollection<T>(IEnumerable<Func<object>> collectionBuilder) where T : class
		{
			return collectionBuilder.Select(builder => builder() as T);
		}

		public IEnumerable ResolveAll(Type serviceType)
		{
			return _collectionRegistrations.TryGetValue(serviceType, out var collectionBuilder)
				? YieldCollection(collectionBuilder)
				: Enumerable.Empty<object>();
		}

		private static IEnumerable YieldCollection(IEnumerable<Func<object>> collectionBuilder)
		{
			return collectionBuilder.Select(builder => builder());
		}

		#endregion

		#region Auto-Resolve w/ Fallback

		private object AutoResolve(Type serviceType, bool throwIfCantResolve)
		{
			while (true)
			{
				// got it:
				if (_registrations.TryGetValue(serviceType, out var creator))
					return creator();

				// want it:
				var typeInfo = serviceType.GetTypeInfo();
				if (!typeInfo.IsAbstract)
					return CreateInstance(serviceType);

				// need it:
				var fallback = _fallbackProvider?.GetService(serviceType);
				if (fallback != null)
					return fallback;

				var type = _fallbackAssemblies.SelectMany(s => s.GetTypes())
					.FirstOrDefault(i => serviceType.IsAssignableFrom(i) && !i.GetTypeInfo().IsInterface);

				if (type == null)
				{
					if (throwIfCantResolve)
						throw new InvalidOperationException($"No registration for {serviceType}");
					return null;
				}

				serviceType = type;
			}
		}

		private object CreateInstance(Type implementationType)
		{
			// type->constructor
			var ctor = GetOrCacheConstructorForType(implementationType);

			// constructor->parameters
			var parameters = GetOrCacheParametersForConstructor(ctor);

			// parameter-less ctor
			if (parameters.Length == 0)
				return Instancing.CreateInstance(implementationType);

			// auto-resolve widest ctor
			var args = new object[parameters.Length];
			for (var i = 0; i < parameters.Length; i++)
				args[i] = AutoResolve(parameters[i].ParameterType, ThrowIfCantResolve);

			return Instancing.CreateInstance(implementationType, args);
		}

		#endregion

		#region Lifetime Management

		private Func<IDependencyResolver, T> WrapLifecycle<T>(Func<IDependencyResolver, T> builder, Lifetime lifetime)
			where T : class
		{
			Func<IDependencyResolver, T> registration;
			switch (lifetime)
			{
				case Lifetime.AlwaysNew:
					registration = builder;
					break;
				case Lifetime.Permanent:
					registration = ProcessMemoize(builder);
					break;
				case Lifetime.Thread:
					registration = ThreadMemoize(builder);
					break;
				case Lifetime.Request:
					foreach (var extension in _extensions)
						if (extension.CanResolve(lifetime))
							return extension.Memoize(this, builder);
					throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime,
						"No extensions can serve this lifetime.");
				default:
					throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
			}

			return registration;
		}

		private Func<T> WrapLifecycle<T>(Func<T> builder, Lifetime lifetime) where T : class
		{
			Func<T> registration;
			switch (lifetime)
			{
				case Lifetime.AlwaysNew:
					registration = builder;
					break;
				case Lifetime.Permanent:
					registration = ProcessMemoize(builder);
					break;
				case Lifetime.Thread:
					registration = ThreadMemoize(builder);
					break;
				case Lifetime.Request:
					foreach (var extension in _extensions)
						if (extension.CanResolve(lifetime))
							return extension.Memoize(this, builder);
					throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime,
						"No extensions can serve this lifetime.");
				default:
					throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
			}

			return registration;
		}

		private static Func<T> ProcessMemoize<T>(Func<T> f)
		{
			var cache = new ConcurrentDictionary<Type, T>();

			return () => cache.GetOrAdd(typeof(T), v => f());
		}

		private static Func<T> ThreadMemoize<T>(Func<T> f)
		{
			var cache = new ThreadLocal<T>(f);

			return () => cache.Value;
		}

		private Func<IDependencyResolver, T> ProcessMemoize<T>(Func<IDependencyResolver, T> f)
		{
			var cache = new ConcurrentDictionary<Type, T>();

			return r => cache.GetOrAdd(typeof(T), v => f(this));
		}

		private Func<IDependencyResolver, T> ThreadMemoize<T>(Func<IDependencyResolver, T> f)
		{
			var cache = new ThreadLocal<T>(() => f(this));

			return r => cache.Value;
		}

		#endregion

		#region Scoping Features

		public IServiceProvider Populate(IServiceCollection services)
		{
			Register<IServiceProvider>(() => new NoServiceProvider(this, services), Lifetime.Permanent);
			Register<IServiceScopeFactory>(() => new NoServiceScopeFactory(this), Lifetime.Permanent);
			Register<IEnumerable<ServiceDescriptor>>(services);
			Register(this);
			return Resolve<IServiceProvider>();
		}

		internal sealed class NoServiceScopeFactory : IServiceScopeFactory
		{
			private readonly IContainer _container;

			public NoServiceScopeFactory(IContainer container) => _container = container;

			public IServiceScope CreateScope()
			{
				return new NoServiceScope(_container);
			}

			private class NoServiceScope : IServiceScope
			{
				private readonly IContainer _container;

				public NoServiceScope(IContainer container) => _container = container;

				public IServiceProvider ServiceProvider => _container.Resolve<IServiceProvider>();

				public void Dispose()
				{
					_container.Dispose();
				}
			}
		}

		internal sealed class NoServiceProvider : IServiceProvider, ISupportRequiredService
		{
			private readonly IContainer _container;
			private readonly IServiceProvider _fallback;

			public NoServiceProvider(IContainer container, IServiceCollection services)
			{
				_container = container;
				_fallback = services.BuildServiceProvider();
				RegisterServiceDescriptors(services);
			}

			public object GetService(Type serviceType)
			{
				return _container.Resolve(serviceType) ?? _fallback.GetService(serviceType);
			}

			public object GetRequiredService(Type serviceType)
			{
				return _container.Resolve(serviceType) ?? _fallback.GetRequiredService(serviceType);
			}

			private void RegisterServiceDescriptors(IServiceCollection services)
			{
				// we're going to shell out to the native container for anything passed in here
				foreach (var descriptor in services)
					_container.Register(descriptor.ServiceType, () => _fallback.GetService(descriptor.ServiceType));
			}
		}

		#endregion

		#region Caches

		private readonly IDictionary<ConstructorInfo, ParameterInfo[]> _constructorParameters =
			new ConcurrentDictionary<ConstructorInfo, ParameterInfo[]>();

		private readonly IDictionary<Type, ConstructorInfo> _constructors =
			new ConcurrentDictionary<Type, ConstructorInfo>();

		public ConstructorInfo GetOrCacheConstructorForType(Type type)
		{
			// type->constructor
			if (!_constructors.TryGetValue(type, out var ctor))
				_constructors[type] = ctor = type.GetWidestConstructor();
			return ctor;
		}

		public ParameterInfo[] GetOrCacheParametersForConstructor(ConstructorInfo ctor)
		{
			// constructor->parameters
			if (!_constructorParameters.TryGetValue(ctor, out var parameters))
				_constructorParameters[ctor] = parameters = ctor.GetParameters();
			return parameters;
		}

		#endregion
	}
}