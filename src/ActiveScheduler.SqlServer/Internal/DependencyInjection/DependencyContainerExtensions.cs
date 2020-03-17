// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	public static class DependencyContainerExtensions
	{
		public static bool AddAspNetCore(this DependencyContainer container)
		{
			return container.AddExtension(new HttpAccessorExtension());
		}
	}
}