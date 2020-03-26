// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ActiveScheduler.Models;
using ActiveScheduler.Tests.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ActiveScheduler.Tests.Fixtures
{
	public class InMemoryFixture : IServiceFixture
	{
		public void Dispose() { }

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddBackgroundTasks();
		}

		public IServiceProvider ServiceProvider { get; set; }

		public void StartIsolation()
		{
			if (ServiceProvider.GetRequiredService<IBackgroundTaskStore>() is InMemoryBackgroundTaskStore memory)
				memory.Clear();
		}

		public void EndIsolation()
		{
			if (ServiceProvider.GetRequiredService<IBackgroundTaskStore>() is InMemoryBackgroundTaskStore memory)
				memory.Clear();
		}
	}
}