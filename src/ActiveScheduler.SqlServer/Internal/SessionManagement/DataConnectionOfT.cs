// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public class DataConnection<TOwner> : DataConnection, IDataConnection<TOwner>
	{
		public DataConnection(DataContext current, IServiceProvider serviceProvider,
			Action<IDbCommand, Type, IServiceProvider> onCommand) : base(current, serviceProvider, onCommand)
		{
		}
	}
}