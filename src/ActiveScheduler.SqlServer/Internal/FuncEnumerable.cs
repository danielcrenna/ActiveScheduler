// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentMigrator.Infrastructure;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;

namespace ActiveScheduler.SqlServer.Internal
{
	internal sealed class NamespaceMigrationInformationLoader : IMigrationInformationLoader
	{
		private readonly DefaultMigrationInformationLoader _inner;
		private readonly string _namespace;
		private readonly IFilteringMigrationSource _source;

		public NamespaceMigrationInformationLoader(string @namespace,
			IFilteringMigrationSource source, DefaultMigrationInformationLoader inner)
		{
			_namespace = @namespace;
			_source = source;
			_inner = inner;
		}

		public SortedList<long, IMigrationInfo> LoadMigrations()
		{
			var migrations =
				_source.GetMigrations(type => type.Namespace == _namespace)
					.Select(_inner.Conventions.GetMigrationInfoForMigration);

			var list = new SortedList<long, IMigrationInfo>();
			foreach (var entry in migrations)
			{
				list.Add(entry.Version, entry);
			}

			return list;
		}
	}

	public struct FuncEnumerable<T, TResult>
	{
		private readonly Func<T, TResult> _func;

		public FuncEnumerable(List<T> inner, Func<T, TResult> func)
		{
			AsList = inner;
			_func = func;
		}

		public FuncEnumerator<T, TResult> GetEnumerator()
		{
			return new FuncEnumerator<T, TResult>(AsList, _func);
		}

		public List<T> AsList { get; }
	}
}