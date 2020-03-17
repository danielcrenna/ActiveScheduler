// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public class DataContext : IDisposable
	{
		private static readonly object Sync = new object();

		private readonly IConnectionFactory _connectionFactory;
		private readonly Action<IDbConnection, IServiceProvider> _onConnection;
		private readonly IServiceProvider _serviceProvider;

		private volatile IDbConnection _connection;

		public DataContext(IConnectionFactory connectionFactory, IServiceProvider serviceProvider,
			Action<IDbConnection, IServiceProvider> onConnection = null)
		{
			_connectionFactory = connectionFactory;
			_serviceProvider = serviceProvider;
			_onConnection = onConnection;
		}

		public IDbConnection Connection => GetConnection();

		public void Dispose()
		{
			_connection?.Dispose();
			_connection = null;
		}

		private IDbConnection GetConnection()
		{
			PrimeConnection();
			return _connection;
		}

		protected void PrimeConnection()
		{
			if (_connection != null) return;
			lock (Sync)
			{
				if (_connection != null) return;
				var connection = _connectionFactory.CreateConnection();
				_onConnection?.Invoke(connection, _serviceProvider);
				connection.Open();
				_connection = connection;
			}
		}
	}
}