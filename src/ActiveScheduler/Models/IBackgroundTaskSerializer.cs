// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ActiveScheduler.Models
{
	public interface IBackgroundTaskSerializer
	{
		string Serialize(object instance);
		T Deserialize<T>(string serialized);
		object Deserialize(string serialized, Type type);
	}
}