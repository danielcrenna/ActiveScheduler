// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;

namespace ActiveScheduler.Models
{
	public class JsonBackgroundTaskSerializer : IBackgroundTaskSerializer
	{
		public string Serialize(object instance)
		{
			return JsonSerializer.Serialize(instance);
		}

		public T Deserialize<T>(string serialized)
		{
			return JsonSerializer.Deserialize<T>(serialized);
		}

		public object Deserialize(string serialized, Type type)
		{
			return JsonSerializer.Deserialize(serialized, type);
		}
	}
}