// Copyright (c) Daniel Crenna & Contributors. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using TypeKitchen;

namespace ActiveScheduler.Api.Internal
{
	internal sealed class RequiredOnlyOneAttribute : RequiredAttribute
	{
		public RequiredOnlyOneAttribute()
		{
			MoreThanOneErrorMessage = "You may only provide one of {0}";
			ErrorMessage = "You must provide one of {0}";
		}

		public string MoreThanOneErrorMessage { get; set; }

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			var present = BaseIsValid(value, validationContext) == null;
			var accessor = ReadAccessor.Create(validationContext.ObjectType, out var members);

			return ValidateSiblings(validationContext, members, accessor, present);
		}

		private ValidationResult ValidateSiblings(ValidationContext validationContext, AccessorMembers members,
			ITypeReadAccessor accessor, bool present)
		{
			var provided = Pooling.HashSetPool.Get();
			var all = Pooling.HashSetPool.Get();
			try
			{
				foreach (var member in members)
				{
					if (member.Name == validationContext.MemberName ||
					    member.MemberType != AccessorMemberType.Property &&
					    member.MemberType != AccessorMemberType.Field ||
					    !member.CanRead || !member.TryGetAttribute<RequiredOnlyOneAttribute>(out var attribute))
						continue;

					accessor.TryGetValue(validationContext.ObjectInstance, member.Name, out var memberValue);

					var memberContext =
						new ValidationContext(validationContext.ObjectInstance) {MemberName = member.Name};

					all.Add(memberContext.DisplayName);

					var result = attribute.BaseIsValid(memberValue, memberContext);
					if (result == null)
						provided.Add(memberContext.DisplayName);
				}

				switch (provided.Count)
				{
					case 0 when present:
					case 1 when !present:
						return ValidationResult.Success;
					case 0:
						all.Add(validationContext.DisplayName);
						return new ValidationResult(string.Format(CultureInfo.CurrentCulture, ErrorMessageString,
							string.Join(", ", all.OrderBy(x => x))));
					default:
						provided.Add(validationContext.DisplayName);
						return new ValidationResult(string.Format(CultureInfo.CurrentCulture, MoreThanOneErrorMessage,
							string.Join(", ", provided.OrderBy(x => x))));
				}
			}
			finally
			{
				Pooling.HashSetPool.Return(provided);
				Pooling.HashSetPool.Return(all);
			}
		}

		private ValidationResult BaseIsValid(object value, ValidationContext validationContext)
		{
			return base.IsValid(value, validationContext);
		}
	}
}