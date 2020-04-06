using System;
using ActiveLogging;
using ActiveScheduler.Models;
using ActiveStorage.Azure.Cosmos;
using ActiveStorage.Azure.Cosmos.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.Azure.Cosmos
{
	public static class Add
	{
		public static BackgroundTaskBuilder AddCosmosBackgroundTasksStore(this BackgroundTaskBuilder builder,
			IConfiguration configuration = null)
		{
			return builder.AddCosmosBackgroundTasksStore(configuration.Bind);
		}

		public static BackgroundTaskBuilder AddCosmosBackgroundTasksStore(this BackgroundTaskBuilder builder,
			string connectionString)
		{
			return builder.AddCosmosBackgroundTasksStore(o => { DefaultDbOptions(connectionString, o); });
		}

		private static void DefaultDbOptions(string connectionString, CosmosStorageOptions o)
		{
			var connectionStringBuilder = new CosmosConnectionStringBuilder(connectionString);

			o.AccountKey = connectionStringBuilder.AccountKey;
			o.AccountEndpoint = connectionStringBuilder.AccountEndpoint;
			o.ContainerId = connectionStringBuilder.DefaultContainer ?? "BackgroundTasks";
			o.DatabaseId = connectionStringBuilder.Database ?? "Default";

			o.SharedCollection = true; // Sequence, Document, etc.
			o.PartitionKeyPaths = new[] {"/id"};
		}

		public static BackgroundTaskBuilder AddCosmosBackgroundTasksStore(this BackgroundTaskBuilder builder,
			Action<CosmosStorageOptions> configureAction = null)
		{
			const string slot = Constants.ConnectionSlots.BackgroundTasks;

			if (configureAction != null)
				builder.Services.Configure(slot, configureAction);

			var dbOptions = new CosmosStorageOptions();
			configureAction?.Invoke(dbOptions);

			var container = MigrateToLatest(slot, new OptionsMonitorShim<CosmosStorageOptions>(dbOptions));

			builder.Services.AddSafeLogging();
			builder.Services.AddSingleton(r => new CosmosRepository(slot, container, r.GetRequiredService<IOptionsMonitor<CosmosStorageOptions>>(), r.GetService<ISafeLogger<CosmosRepository>>()));
			builder.Services.Replace(ServiceDescriptor.Singleton<IBackgroundTaskStore, CosmosBackgroundTaskStore>());

			return builder;
		}

		private static Container MigrateToLatest(string slot, IOptionsMonitor<CosmosStorageOptions> optionsMonitor)
		{
			var runner = new CosmosMigrationRunner(slot, optionsMonitor);
			return runner.CreateContainerIfNotExistsAsync().GetAwaiter().GetResult();
		}
	}
}
