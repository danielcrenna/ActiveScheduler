// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace ActiveScheduler.Internal
{
	internal class BackgroundTaskService : IHostedService
	{
		private readonly BackgroundTaskHost _host;

		public BackgroundTaskService(BackgroundTaskHost host) => _host = host;

		public Task StartAsync(CancellationToken cancellationToken)
		{
			_host.Start();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			_host.Stop(cancellationToken);
			return Task.CompletedTask;
		}
	}
}