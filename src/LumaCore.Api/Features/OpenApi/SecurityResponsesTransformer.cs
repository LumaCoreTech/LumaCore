// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LumaCore.Api.Features.OpenApi;

/// <summary>
/// An <see cref="IOpenApiOperationTransformer"/> that automatically documents common error responses.
/// </summary>
/// <remarks>
///     <para>
///     This transformer analyzes endpoint metadata and automatically adds appropriate
///     error response documentation to the OpenAPI specification:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///             <b>400 Bad Request</b> – Added when the endpoint accepts a request body,
///             indicating that validation errors may occur.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>401 Unauthorized</b> – Added when the endpoint requires authentication
///             (has <see cref="AuthorizeAttribute"/> metadata). Despite the historical name
///             "Unauthorized", this status indicates missing or invalid authentication.
///             </description>
///         </item>
///         <item>
///             <description>
///             <b>403 Forbidden</b> – Added when the endpoint requires specific roles
///             or policies, indicating that authenticated users may still be denied access
///             due to insufficient authorization.
///             </description>
///         </item>
///     </list>
///     <para>
///     This transformer ensures consistent error response documentation across all endpoints
///     without requiring developers to manually add <c>.Produces()</c> calls for common
///     error scenarios.
///     </para>
/// </remarks>
/// <example>
/// Register this transformer in <c>Program.Services.cs</c>:
/// <code>
/// builder.Services.AddOpenApi("v1", options =>
/// {
///     options.AddOperationTransformer&lt;SecurityResponsesTransformer&gt;();
/// });
/// </code>
/// </example>
public sealed class SecurityResponsesTransformer : IOpenApiOperationTransformer
{
	/// <summary>
	/// Transforms the <see cref="OpenApiOperation"/> by adding error response documentation.
	/// </summary>
	/// <param name="operation">The <see cref="OpenApiOperation"/> to transform.</param>
	/// <param name="context">
	/// The <see cref="OpenApiOperationTransformerContext"/> containing endpoint metadata.
	/// </param>
	/// <param name="cancellationToken">
	/// A <see cref="CancellationToken"/> to cancel the operation.
	/// </param>
	/// <returns>A completed <see cref="Task"/>.</returns>
	public Task TransformAsync(
		OpenApiOperation                   operation,
		OpenApiOperationTransformerContext context,
		CancellationToken                  cancellationToken)
	{
		// Get endpoint metadata for analysis.
		IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

		// Add 400 Bad Request if the endpoint has a request body.
		// This indicates that validation errors may occur.
		if (HasRequestBody(operation))
		{
			AddOrUpdateResponse(
				operation,
				"400",
				"The request body is invalid or failed validation.");
		}

		// Check for authorization requirements.
		IAuthorizeData? authorizeData = metadata.OfType<IAuthorizeData>().FirstOrDefault();
		bool allowAnonymous = metadata.OfType<IAllowAnonymous>().Any();

		// Add 401 Unauthorized if the endpoint requires authentication.
		// Note: Despite the name "Unauthorized", HTTP 401 indicates an authentication
		// failure (who are you?), while 403 indicates an authorization failure (are you allowed?).
		if (authorizeData is not null && !allowAnonymous)
		{
			AddOrUpdateResponse(
				operation,
				"401",
				"Authentication is required to access this endpoint.");

			// Add 403 Forbidden if the endpoint requires specific roles or policies.
			// This indicates that even authenticated users may be denied access.
			if (HasRolesOrPolicies(authorizeData))
			{
				AddOrUpdateResponse(
					operation,
					"403",
					"The authenticated user does not have permission to access this resource.");
			}
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Determines whether the <see cref="OpenApiOperation"/> has a request body.
	/// </summary>
	/// <param name="operation">The <see cref="OpenApiOperation"/> to check.</param>
	/// <returns>
	/// <see langword="true"/> if the operation has a request body; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool HasRequestBody(OpenApiOperation operation)
	{
		return operation.RequestBody is not null;
	}

	/// <summary>
	/// Determines whether the <see cref="IAuthorizeData"/> includes roles or policies.
	/// </summary>
	/// <param name="authorizeData">The <see cref="IAuthorizeData"/> to check.</param>
	/// <returns>
	/// <see langword="true"/> if roles or policies are specified; otherwise, <see langword="false"/>.
	/// </returns>
	private static bool HasRolesOrPolicies(IAuthorizeData authorizeData)
	{
		return !string.IsNullOrWhiteSpace(authorizeData.Roles) ||
		       !string.IsNullOrWhiteSpace(authorizeData.Policy);
	}

	/// <summary>
	/// Adds or updates an <see cref="OpenApiResponse"/> in the operation.
	/// </summary>
	/// <param name="operation">The <see cref="OpenApiOperation"/> to modify.</param>
	/// <param name="statusCode">The HTTP status code as a string (e.g., <c>"400"</c>).</param>
	/// <param name="description">The response description.</param>
	/// <remarks>
	///     <para>
	///     If a response for the given status code already exists with a generic description
	///     (e.g., <c>"Unauthorized"</c>, <c>"Forbidden"</c>, <c>"Bad Request"</c>), it will be
	///     replaced with the more descriptive text. Custom descriptions are preserved.
	///     </para>
	/// </remarks>
	private static void AddOrUpdateResponse(
		OpenApiOperation operation,
		string           statusCode,
		string           description)
	{
		// Ensure Responses collection exists.
		operation.Responses ??= new OpenApiResponses();

		// Check if response already exists.
		if (operation.Responses.TryGetValue(statusCode, out IOpenApiResponse? existing))
		{
			// Only override generic HTTP reason phrases with our more descriptive text.
			// This preserves any custom descriptions set by endpoint authors.
			string? existingDesc = existing.Description;
			bool isGeneric = string.IsNullOrWhiteSpace(existingDesc) ||
			                 existingDesc is "OK" or "Unauthorized" or "Forbidden" or "Bad Request" or "Not Found";

			if (!isGeneric) return;
		}

		operation.Responses[statusCode] = new OpenApiResponse
		{
			Description = description
		};
	}
}
