// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveLogging;
using ActiveOptions;
using ActiveRoutes;
using ActiveScheduler.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.Api
{
	public static class Add
	{
		public static IServiceCollection AddBackgroundTasksApi(this IServiceCollection services, IConfiguration config)
		{
			return AddBackgroundTasksApi(services, config.FastBind);
		}

		public static IServiceCollection AddBackgroundTasksApi(this IServiceCollection services,
			Action<BackgroundTaskOptions> configureTasks = null)
		{
			services.AddMvcCore().AddBackgroundTasksApi(configureTasks);
			return services;
		}

		public static IMvcCoreBuilder AddBackgroundTasksApi(this IMvcCoreBuilder mvcBuilder, IConfiguration config)
		{
			return AddBackgroundTasksApi(mvcBuilder, config.FastBind);
		}

		public static IMvcCoreBuilder AddBackgroundTasksApi(this IMvcCoreBuilder mvcBuilder,
			Action<BackgroundTaskOptions> configureTasks = null)
		{
			mvcBuilder.Services.Configure(configureTasks);
			mvcBuilder.Services.AddSafeLogging();

			mvcBuilder.AddActiveRoute<BackgroundTaskController, BackgroundTasksFeature, BackgroundTaskOptions>();
			return mvcBuilder;
		}
	}
}