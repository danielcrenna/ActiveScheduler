// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace ActiveScheduler.SqlServer.Internal
{
	internal struct SelfEnumerable<T>
	{
		public SelfEnumerable(List<T> inner) => AsList = inner;

		public SelfEnumerator<T> GetEnumerator()
		{
			return new SelfEnumerator<T>(AsList);
		}

		public List<T> AsList { get; }
	}
}