// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public class ProxyConnectionFactory : ConnectionFactory
	{
		public Func<string, IDbConnection> Proxy { get; set; }

		public override IDbConnection CreateConnection()
		{
			return Proxy(ConnectionString);
		}
	}
}