// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;

namespace ActiveScheduler.Internal
{
	internal sealed class ComputedAttribute : DatabaseGeneratedAttribute
	{
		public ComputedAttribute() : base(DatabaseGeneratedOption.Computed) { }
	}
}