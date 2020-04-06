// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ActiveScheduler.Api;
using ActiveScheduler.Tests.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActiveScheduler.Tests
{
	public class CreateBackgroundTaskModelTests : UnitUnderTest
	{
		[Fact]
		public void Can_validate()
		{
			var model = new CreateBackgroundTaskModel();
			model.TaskCode = null;
			model.TaskType = null;

			var serviceCollection = new ServiceCollection();
			var serviceProvider = serviceCollection.BuildServiceProvider();

			var context = new ValidationContext(model, serviceProvider, null);
			var results = new List<ValidationResult>();
			Validator.TryValidateObject(model, context, results, true);
		}
	}
}