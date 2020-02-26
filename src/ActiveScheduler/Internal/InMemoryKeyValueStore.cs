// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;

namespace ActiveScheduler.Internal
{
	internal sealed class InMemoryKeyValueStore<TKey, TValue> : IKeyValueStore<TKey, TValue>
	{
		private readonly ConcurrentDictionary<TKey, TValue> _memory;

		public InMemoryKeyValueStore() => _memory = new ConcurrentDictionary<TKey, TValue>();

		public TValue GetOrAdd(TKey name, TValue metric)
		{
			return _memory.GetOrAdd(name, metric);
		}

		public TValue this[TKey name] => _memory[name];

		public bool TryGetValue(TKey name, out TValue value)
		{
			return _memory.TryGetValue(name, out value);
		}

		public bool Contains(TKey name)
		{
			return _memory.ContainsKey(name);
		}

		public void AddOrUpdate<T>(TKey name, T value) where T : TValue
		{
			_memory.AddOrUpdate(name, value, (n, m) => m);
		}

		public bool Clear()
		{
			_memory.Clear();
			return true;
		}
	}
}