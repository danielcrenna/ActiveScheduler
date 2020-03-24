// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using ActiveScheduler.Api.Internal;

namespace ActiveScheduler.Api
{
	public class CreateBackgroundTaskModel
	{
		[RequiredOnlyOne] public string TaskType { get; set; }

		[RequiredOnlyOne] public string TaskCode { get; set; }

		public string[] Tags { get; set; }

		public string Expression { get; set; }

		public object Data { get; set; }
	}
}