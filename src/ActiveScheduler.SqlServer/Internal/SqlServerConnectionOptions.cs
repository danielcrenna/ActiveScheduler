using ActiveConnection;
using ActiveScheduler.Configuration;
using Microsoft.Extensions.Options;

namespace ActiveScheduler.SqlServer.Internal
{
	public class SqlServerConnectionOptions : IDbConnectionOptions, IOptions<SqlServerConnectionOptions>
	{
		private readonly StoreOptions _options;

		public bool CreateIfNotExists => _options.CreateIfNotExists;
		public bool MigrateOnStartup => _options.MigrateOnStartup;

		public SqlServerConnectionOptions()
		{ }


		public SqlServerConnectionOptions(StoreOptions options)
		{
			_options = options;
		}

		public SqlServerConnectionOptions Value => this;
	}
}