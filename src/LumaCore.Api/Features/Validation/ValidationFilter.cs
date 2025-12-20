// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.ErrorHandling;

using Microsoft.AspNetCore.Http.HttpResults;

namespace LumaCore.Api.Features.Validation;

/// <summary>
/// An <see cref="IEndpointFilter"/> that validates request parameters using
/// <see cref="System.ComponentModel.DataAnnotations"/>.
/// </summary>
/// <remarks>
///     <para>
///     This filter automatically validates all endpoint arguments that have
///     <see cref="System.ComponentModel.DataAnnotations"/> attributes (such as
///     <see cref="RequiredAttribute"/>, <see cref="RangeAttribute"/>,
///     <see cref="StringLengthAttribute"/>, etc.) and returns a <c>400 Bad Request</c> with
///     a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> response if validation fails.
///     </para>
///     <para>
///     The filter can be applied to individual endpoints or to entire route groups:
///     <code>
///     // Single endpoint
///     app.MapPost("/items", HandleCreate)
///        .AddEndpointFilter&lt;ValidationFilter&gt;();
/// 
///     // Route group (all endpoints in the group)
///     app.MapGroup("/api/v1/items")
///        .AddEndpointFilter&lt;ValidationFilter&gt;()
///        .MapPost("/", HandleCreate)
///        .MapPut("/{id}", HandleUpdate);
///     </code>
///     </para>
///     <para>
///     This filter works in conjunction with the <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>
///     middleware configured in <c>Program.cs</c>. Validation errors are returned as RFC 7807
///     compliant responses with detailed information about which properties failed validation.
///     </para>
/// </remarks>
/// <example>
/// Define a request record with validation attributes:
/// <code>
/// public sealed record CreateItemRequest(
///     [Required, StringLength(100)] string Name,
///     [Range(0, 10000)] decimal Price);
/// </code>
/// Then apply the filter to the endpoint:
/// <code>
/// app.MapPost("/items", (CreateItemRequest request) => ...)
///    .AddEndpointFilter&lt;ValidationFilter&gt;();
/// </code>
/// Invalid requests will automatically receive a 400 response with validation details.
/// </example>
sealed class ValidationFilter : IEndpointFilter
{
	/// <summary>
	/// Validates all endpoint arguments and either continues the pipeline or returns a validation error.
	/// </summary>
	/// <param name="context">
	/// The <see cref="EndpointFilterInvocationContext"/> containing the arguments to validate.
	/// </param>
	/// <param name="next">
	/// The <see cref="EndpointFilterDelegate"/> to invoke if validation succeeds.
	/// </param>
	/// <returns>
	/// The result of calling <paramref name="next"/> if validation passes, or an
	/// <see cref="IResult"/> containing a <see cref="ValidationProblem"/> response if validation fails.
	/// </returns>
	public async ValueTask<object?> InvokeAsync(
		EndpointFilterInvocationContext context,
		EndpointFilterDelegate          next)
	{
		// Collect validation errors from all arguments.
		Dictionary<string, string[]>? errors = null;

		foreach (object? argument in context.Arguments)
		{
			// Skip null arguments – they may be optional parameters.
			// Required validation is handled by the [Required] attribute.
			if (argument is null)
				continue;

			// Validate the argument using DataAnnotations.
			List<ValidationResult> results = [];
			var validationContext = new ValidationContext(argument);

			if (!Validator.TryValidateObject(argument, validationContext, results, validateAllProperties: true))
			{
				// Initialize the errors dictionary on first validation failure.
				errors ??= new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

				// Group validation errors by member name.
				foreach (ValidationResult result in results)
				{
					string memberName = result.MemberNames.FirstOrDefault() ?? string.Empty;
					string errorMessage = result.ErrorMessage ?? "Validation failed.";

					if (errors.TryGetValue(memberName, out string[]? existingErrors))
					{
						// Append to existing errors for this member.
						errors[memberName] = [.. existingErrors, errorMessage];
					}
					else
					{
						errors[memberName] = [errorMessage];
					}
				}
			}
		}

		// If any validation errors were found, return a ValidationProblem response.
		// This uses the ProblemDetails infrastructure for consistent error formatting
		// with the LumaCore-specific validation error type URN.
		if (errors is not null)
		{
			return Results.ValidationProblem(errors, type: ErrorTypes.Validation);
		}

		// Validation passed – continue with the endpoint handler.
		return await next(context).ConfigureAwait(false);
	}
}
