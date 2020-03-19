// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.Common;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public class DataConnection : IDataConnection
	{
		private readonly DataContext _current;

		private volatile IDbTransaction _transaction;

		public DataConnection(DataContext current)
		{
			_current = current;
		}

		public IDbConnection Current => _current.Connection;
		public IDbTransaction Transaction => _transaction;

		public void SetTransaction(IDbTransaction transaction)
		{
			_transaction = transaction;
		}
	}
}