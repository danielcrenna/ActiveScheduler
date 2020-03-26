// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveLogging;
using ActiveScheduler.Configuration;
using ActiveScheduler.Internal;
using ActiveScheduler.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TypeKitchen;

namespace ActiveScheduler
{
	public static class Add
	{
		public static BackgroundTaskBuilder AddBackgroundTasks(this IServiceCollection services,
			IConfiguration configuration)
		{
			return services.AddBackgroundTasks(configuration.Bind);
		}

		public static BackgroundTaskBuilder AddBackgroundTasks(this IServiceCollection services,
			Action<BackgroundTaskOptions> configureAction = null)
		{
			if (configureAction != null)
				services.Configure(configureAction);

			services.AddTypeResolver();
			services.AddSafeLogging();

			services.TryAddSingleton<IBackgroundTaskStore>(r =>
				new InMemoryBackgroundTaskStore(r, _ => DateTimeOffset.Now));
			services.TryAddSingleton<IBackgroundTaskSerializer, JsonBackgroundTaskSerializer>();
			services.TryAddSingleton<BackgroundTaskHost>();

			services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BackgroundTaskService>());

			return new BackgroundTaskBuilder(services);
		}
	}
}