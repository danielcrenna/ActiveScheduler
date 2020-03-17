// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace ActiveScheduler.SqlServer.Internal.DependencyInjection
{
	internal struct NameAndType
	{
		public readonly Type Type;
		public readonly string Name;

		public NameAndType(string name, Type type)
		{
			Name = name;
			Type = type;
		}

		public bool Equals(NameAndType other)
		{
			return Type == other.Type && string.Equals(Name, other.Name);
		}

		public override bool Equals(object obj)
		{
			return !ReferenceEquals(null, obj) && obj is NameAndType andType && Equals(andType);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Type?.GetHashCode() ?? 0) * 397) ^ (Name?.GetHashCode() ?? 0);
			}
		}

		private sealed class TypeNameEqualityComparer : IEqualityComparer<NameAndType>
		{
			public bool Equals(NameAndType x, NameAndType y)
			{
				return x.Type == y.Type && string.Equals(x.Name, y.Name);
			}

			public int GetHashCode(NameAndType obj)
			{
				unchecked
				{
					return ((obj.Type?.GetHashCode() ?? 0) * 397) ^ (obj.Name?.GetHashCode() ?? 0);
				}
			}
		}

		public static IEqualityComparer<NameAndType> TypeNameComparer { get; } = new TypeNameEqualityComparer();
	}
}