// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ActiveScheduler.Internal
{
	internal interface IKeyValueStore<in TKey, TValue>
	{
		TValue this[TKey key] { get; }
		TValue GetOrAdd(TKey key, TValue value);
		bool TryGetValue(TKey key, out TValue value);
		bool Contains(TKey key);
		void AddOrUpdate<T>(TKey key, T value) where T : TValue;
		bool Clear();
	}
}