// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.TraceSource;
using Xunit.Sdk;

namespace ActiveScheduler.Tests.Internal
{
	public sealed class XunitOutputProvider : IOutputProvider
	{
		internal TestOutputHelper Inner;

		public XunitOutputProvider() => Inner = new TestOutputHelper();

		public bool IsAvailable => Inner != null;

		public void WriteLine(string message)
		{
			Inner.WriteLine(message);
		}

		public void WriteLine(string format, params object[] args)
		{
			Inner.WriteLine(format, args);
		}
	}

	public interface IOutputProvider
	{
		bool IsAvailable { get; }
		void WriteLine(string message);
		void WriteLine(string format, params object[] args);
	}

	public static class AmbientContext
	{
		public static IOutputProvider OutputProvider { get; set; }
	}

	public abstract class ServiceUnderTest : TestScope, IDisposable
	{
		protected ILogger<ServiceUnderTest> Logger;

		protected ServiceUnderTest() : this(null) { }

		protected ServiceUnderTest(IServiceProvider serviceProvider) => Initialize(serviceProvider);

		public static IServiceFixture AmbientServiceFixture { get; private set; }

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Initialize(IServiceProvider serviceProvider)
		{
			if (serviceProvider == null)
				InitializeServiceProvider();
			else
				ServiceProvider = serviceProvider;

			TryInstallLogging();

			TryInstallTracing();
		}

		public virtual void ConfigureServices(IServiceCollection services) { }

		protected virtual void InitializeServiceProvider()
		{
			var services = new ServiceCollection();
			ConfigureServices(services);
			ServiceProvider = services.BuildServiceProvider();
		}

		protected internal void TryInstallLogging()
		{
			var loggerFactory = ServiceProvider?.GetService<ILoggerFactory>();
			if (loggerFactory != null)
				return;

			loggerFactory = ServiceProvider?.GetService<ILoggerFactory>();
			loggerFactory ??= DefaultLoggerFactory;
			loggerFactory.AddProvider(CreateLoggerProvider());

			Logger = loggerFactory.CreateLogger<ServiceUnderTest>();
		}

		protected internal void TryInstallTracing()
		{
			if (ServiceProvider?.GetService<TraceSourceLoggerProvider>() != null)
				return;

			Trace.Listeners.Add(new DelegateTraceListener(message =>
			{
				var outputProvider = AmbientContext.OutputProvider;
				if (outputProvider?.IsAvailable != true)
					return;
				outputProvider.WriteLine(message);
			}));
		}

		public override ILogger GetLogger()
		{
			return ServiceProvider?.GetService<ILogger<ServiceUnderTest>>() ?? Logger;
		}

		protected static IServiceProvider CreateServiceProvider(IServiceFixture fixture)
		{
			var services = new ServiceCollection();
			fixture.ConfigureServices(services);
			fixture.ServiceProvider = services.BuildServiceProvider();
			AmbientServiceFixture = fixture;
			return fixture.ServiceProvider;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
			}
		}
	}
}