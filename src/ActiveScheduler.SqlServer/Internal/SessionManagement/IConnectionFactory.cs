// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public interface IConnectionFactory
	{
		string ConnectionString { get; set; }
		IDbConnection CreateConnection();
	}
}