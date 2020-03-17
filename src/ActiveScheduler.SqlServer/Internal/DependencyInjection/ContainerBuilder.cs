// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	public class ContainerBuilder
	{
		private readonly IContainer _container;

		public ContainerBuilder(IServiceCollection services, IContainer container)
		{
			_container = container;
			Services = services;
		}

		public IServiceCollection Services { get; }

		public bool AddExtension<T>(T extension) where T : IResolverExtension
		{
			return _container.AddExtension(extension);
		}
	}
}