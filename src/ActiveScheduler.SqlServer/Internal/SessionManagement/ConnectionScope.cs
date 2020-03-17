// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ActiveScheduler.SqlServer.Internal.SessionManagement
{
	public enum ConnectionScope
	{
		/// <summary>
		///     One connection is opened on every request
		/// </summary>
		AlwaysNew,

		/// <summary>
		///     One connection is opened for a single HTTP request
		/// </summary>
		ByRequest,

		/// <summary>
		///     One connection is opened per thread
		/// </summary>
		ByThread,

		/// <summary>
		///     One connection is opened on first use
		/// </summary>
		KeepAlive
	}
}