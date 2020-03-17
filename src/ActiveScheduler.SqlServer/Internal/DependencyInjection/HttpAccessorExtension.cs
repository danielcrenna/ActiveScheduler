// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	public class HttpAccessorExtension : IResolverExtension
	{
		public bool CanResolve(Lifetime lifetime)
		{
			return lifetime == Lifetime.Request;
		}

		public Func<T> Memoize<T>(IDependencyResolver host, Func<T> f)
		{
			return () =>
			{
				var accessor = host.Resolve<IHttpContextAccessor>();
				if (accessor?.HttpContext == null)
				{
					return f(); // always new
				}

				var cache = accessor.HttpContext.Items;
				var cacheKey = f.ToString();
				if (cache.TryGetValue(cacheKey, out var item))
				{
					return (T) item; // got it
				}

				item = f(); // need it
				cache.Add(cacheKey, item);
				if (item is IDisposable disposable)
				{
					accessor.HttpContext.Response.RegisterForDispose(disposable);
				}

				return (T) item;
			};
		}

		public Func<IDependencyResolver, T> Memoize<T>(IDependencyResolver host, Func<IDependencyResolver, T> f)
		{
			return r =>
			{
				var accessor = r.Resolve<IHttpContextAccessor>();
				if (accessor?.HttpContext == null)
				{
					return f(host); // always new
				}

				var cache = accessor.HttpContext.Items;
				var cacheKey = f.ToString();
				if (cache.TryGetValue(cacheKey, out var item))
				{
					return (T) item; // got it
				}

				item = f(host); // need it
				cache.Add(cacheKey, item);
				if (item is IDisposable disposable)
				{
					accessor.HttpContext.Response.RegisterForDispose(disposable);
				}

				return (T) item;
			};
		}
	}
}