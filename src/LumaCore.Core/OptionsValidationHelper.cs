// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Core.IO;

namespace LumaCore.Core;

/// <summary>
/// Provides a centralized helper for validating <see cref="IValidatableObject"/> instances
/// (typically Options classes) and throwing a <see cref="ValidationException"/> with all
/// errors aggregated into a single message.
/// </summary>
/// <remarks>
///     <para>
///     <b>Why is this in LumaCore.Core and not LumaCore.Configuration?</b>
///     This helper is consumed by types inside <c>LumaCore.Core</c> itself (e.g.
///     <see cref="StreamBufferPoolOptions"/>). Moving it to <c>LumaCore.Configuration</c>
///     would force <c>LumaCore.Core</c> to take a project reference on
///     <c>LumaCore.Configuration</c> — a layering violation, since Configuration sits
///     <em>above</em> Core (DI, binding, tracking) and should depend on Core, not the reverse.
///     The helper uses only <see cref="Validator"/> from the BCL and is therefore a
///     framework-agnostic building block that fits Core's scope.
///     </para>
///     <para>
///     This replaces the copy-pasted <c>ThrowIfInvalid()</c> pattern that was previously
///     duplicated across Options classes. Use it as an extension method:
///     </para>
///     <code>
/// options.ThrowIfInvalid();
///     </code>
///     <para>
///     <b>Why both TryValidateObject and manual Validate()?</b>
///     <see cref="Validator.TryValidateObject(object,ValidationContext,ICollection{ValidationResult},bool)"/>
///     calls <see cref="IValidatableObject.Validate"/> <em>only</em> when all property-level
///     data-annotation attributes pass. If a property-level attribute fails,
///     <c>Validate()</c> is skipped entirely — cross-property constraint violations would be
///     silently swallowed. This helper therefore runs <c>Validate()</c> manually when
///     <c>TryValidateObject</c> reports failure, and merges the results with deduplication.
///     </para>
/// </remarks>
public static class OptionsValidationHelper
{
	/// <summary>
	/// Validates <paramref name="options"/> using <see cref="Validator"/> and throws a
	/// <see cref="ValidationException"/> if any constraint is violated.
	/// </summary>
	/// <param name="options">The options instance to validate.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="ValidationException">One or more validation constraints are violated.</exception>
	public static void ThrowIfInvalid(this IValidatableObject options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var context = new ValidationContext(options);
		var results = new List<ValidationResult>();

		// Step 1: Property-level validation via data annotations (e.g. [Range], [Required]).
		// TryValidateObject calls IValidatableObject.Validate() only when all property-level
		// attributes pass — so cross-property violations may be missing from 'results' here.
		bool isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

		// Step 2: When TryValidateObject failed, run cross-property validation manually
		// and merge unique results. This guarantees that cross-property constraint
		// violations are reported even when a property-level attribute already failed
		// and caused TryValidateObject() to skip IValidatableObject.Validate().
		if (!isValid)
		{
			foreach (ValidationResult customResult in options.Validate(context))
			{
				if (!results.Exists(r => string.Equals(
					    r.ErrorMessage,
					    customResult.ErrorMessage,
					    StringComparison.Ordinal)))
				{
					results.Add(customResult);
				}
			}
		}

		if (results.Count > 0)
		{
			string message = string.Join(
				Environment.NewLine,
				results.ConvertAll(static r => r.ErrorMessage));

			throw new ValidationException($"{options.GetType().Name} validation failed:{Environment.NewLine}{message}");
		}
	}
}
