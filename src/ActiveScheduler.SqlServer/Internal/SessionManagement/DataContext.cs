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

		private volatile IDbConnection _connection;

		public DataContext(IConnectionFactory connectionFactory)
		{
			_connectionFactory = connectionFactory;
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
				connection.Open();
				_connection = connection;
			}
		}
	}
}