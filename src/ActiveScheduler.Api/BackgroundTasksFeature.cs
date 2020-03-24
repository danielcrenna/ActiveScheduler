// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using ActiveRoutes;

namespace ActiveScheduler.Api
{
	internal sealed class BackgroundTasksFeature : DynamicFeature
	{
		public override IList<Type> ControllerTypes { get; } = new[] {typeof(BackgroundTaskController)};
	}

	public static class ErrorEvents
	{
		/// <summary>
		///     The request was improperly structured, to the point it could not be validated.
		/// </summary>
		public const long InvalidRequest = 1001;

		/// <summary>
		///     The request was evaluated, but failed validation
		/// </summary>
		public const long ValidationFailed = 1001;

		/// <summary>
		///     The request is valid, but could expose sensitive data.
		/// </summary>
		public const long UnsafeRequest = 1002;


		public const long FieldDoesNotMatch = 1003;
		public const long AggregateErrors = 1004;
		public const long IdentityError = 1005;
		public const long ResourceMissing = 1006;
		public const long ResourceNotImplemented = 1007;
		public const long InvalidParameter = 1008;
		public const long RequestEntityTooLarge = 1009;
		public const long CouldNotAcceptWork = 1011;

		/// <summary>
		///     The request failed because the platform has an issue.
		/// </summary>
		public const long PlatformError = 2001;
	}
}