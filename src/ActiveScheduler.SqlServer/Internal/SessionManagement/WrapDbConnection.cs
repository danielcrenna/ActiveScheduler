// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Common;
using TypeKitchen;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public class WrapDbConnection : DbConnection
	{
		private readonly IRetainLastInsertedId _maybeRetains;
		private readonly Action<IDbCommand, Type, IServiceProvider> _onCommand;
		private readonly IServiceProvider _serviceProvider;
		private readonly Type _type;

		public WrapDbConnection(DbConnection inner, IServiceProvider serviceProvider,
			Action<IDbCommand, Type, IServiceProvider> onCommand, Type type)
		{
			Inner = inner;
			_serviceProvider = serviceProvider;
			_onCommand = onCommand;
			_type = type;

			try
			{
				_maybeRetains = Inner.QuackLike<IRetainLastInsertedId>();
				_maybeRetains.GetLastInsertedId();
			}
			catch
			{
				_maybeRetains = null;
			}
		}

		public DbConnection Inner { get; }

		public object LastInsertedId =>
			_maybeRetains is IRetainLastInsertedId retainer ? retainer.GetLastInsertedId() : null;

		public override string ConnectionString
		{
			get => Inner.ConnectionString;
			set => Inner.ConnectionString = value;
		}

		public override string Database => Inner.Database;
		public override ConnectionState State => Inner.State;
		public override string DataSource => Inner.DataSource;
		public override string ServerVersion => Inner.ServerVersion;

		protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
		{
			return Inner.BeginTransaction(isolationLevel);
		}

		public override void Close()
		{
			Inner.Close();
		}

		public override void Open()
		{
			Inner.Open();
		}

		protected override DbCommand CreateDbCommand()
		{
			var command = Inner.CreateCommand();
			_onCommand?.Invoke(command, _type, _serviceProvider);
			return new WrapDbCommand(command);
		}

		public override void ChangeDatabase(string databaseName)
		{
			Inner.ChangeDatabase(databaseName);
		}
	}
}