// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using ActiveScheduler.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.Models
{
	public sealed class ExecutionContext : IDisposable
	{
		private readonly IKeyValueStore<string, object> _data;
		private readonly IServiceScope _serviceScope;

		internal ExecutionContext(IServiceScope serviceScope, IKeyValueStore<string, object> data, CancellationToken cancellationToken = default)
		{
			Continue = true;
			Successful = true;

			CancellationToken = cancellationToken;
			_serviceScope = serviceScope;
			_data = data;
		}

		public IServiceProvider ExecutionServices => _serviceScope.ServiceProvider;
		public CancellationToken CancellationToken { get; }

		public bool Continue { get; private set; }
		public bool Successful { get; private set; }

		internal Exception Error { get; private set; }

		#region IDisposable

		public void Dispose()
		{
			_serviceScope.Dispose();
		}

		#endregion

		public void Fail(Exception error = null)
		{
			if (!Continue)
				throw new ExecutionException(this, $"{nameof(Fail)} was called on a halted execution");
			Error = error;
			Continue = false;
			Successful = false;
		}

		public void Succeed()
		{
			if (!Continue)
				throw new ExecutionException(this, $"{nameof(Succeed)} was called on a halted execution");
			Continue = false;
			Successful = true;
		}

		public void AddData<T>(string key, T item)
		{
			_data.AddOrUpdate(key, item);
		}

		public bool TryGetData<T>(string key, out T item) where T : class
		{
			if (_data.TryGetValue(key, out var value))
			{
				item = value as T;
				return item != null;
			}

			item = default;
			return false;
		}

		public bool TryGetData(string key, out object item)
		{
			if (_data.TryGetValue(key, out item))
				return true;
			item = default;
			return false;
		}
	}
}