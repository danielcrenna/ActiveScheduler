// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Xunit.Sdk;

namespace ActiveScheduler.Tests.Internal
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class MethodFixtureAttribute : BeforeAfterTestAttribute
	{
		private object _instance;

		public MethodFixtureAttribute(Type type) => Type = type;

		public Type Type { get; }

		public override void Before(MethodInfo methodUnderTest)
		{
			base.Before(methodUnderTest);

			_instance = Activator.CreateInstance(Type);
		}

		public override void After(MethodInfo methodUnderTest)
		{
			if (_instance is IDisposable disposable)
				disposable.Dispose();

			base.After(methodUnderTest);
		}
	}
}