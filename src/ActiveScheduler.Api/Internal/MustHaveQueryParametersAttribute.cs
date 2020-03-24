// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace ActiveScheduler.Api.Internal
{
	internal sealed class MustHaveQueryParametersAttribute : Attribute, IActionConstraint
	{
		private readonly string[] _matchAll;

		public MustHaveQueryParametersAttribute(params string[] matchAll) => _matchAll = matchAll;

		public int Order => 0;

		public bool Accept(ActionConstraintContext context)
		{
			var query = context.RouteContext.HttpContext.Request.Query;
			if (query.Count != _matchAll.Length) return false;

			foreach (var key in _matchAll)
				if (!query.ContainsKey(key))
					return false;

			return true;
		}
	}
}