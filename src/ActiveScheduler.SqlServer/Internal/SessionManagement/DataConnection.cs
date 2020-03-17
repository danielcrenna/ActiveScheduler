// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Common;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public class DataConnection : IDataConnection
	{
		private readonly DataContext _current;
		private readonly Action<IDbCommand, Type, IServiceProvider> _onCommand;
		private readonly IServiceProvider _serviceProvider;

		private volatile IDbTransaction _transaction;
		private Type _type;

		public DataConnection(DataContext current, IServiceProvider serviceProvider,
			Action<IDbCommand, Type, IServiceProvider> onCommand)
		{
			_current = current;
			_serviceProvider = serviceProvider;
			_onCommand = onCommand;
		}

		public void SetTypeInfo(Type type)
		{
			_type = type;
		}

		public void SetTypeInfo<T>()
		{
			_type = typeof(T);
		}

		public bool TryGetLastInsertedId<TKey>(out TKey key)
		{
			if (!(Current is WrapDbConnection wrapped))
			{
				key = default;
				return false;
			}

			if (wrapped.LastInsertedId == null)
			{
				key = default;
				return false;
			}

			key = (TKey) Convert.ChangeType(wrapped.LastInsertedId, typeof(TKey));
			return true;
		}

		public IDbConnection Current
		{
			get
			{
				if (_onCommand != null && _current.Connection is DbConnection connection)
					return new WrapDbConnection(connection, _serviceProvider, _onCommand, _type);

				return _current.Connection;
			}
		}

		public IDbTransaction Transaction => _transaction;

		public void SetTransaction(IDbTransaction transaction)
		{
			_transaction = transaction;
		}
	}
}