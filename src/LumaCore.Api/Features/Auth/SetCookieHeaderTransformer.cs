// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace LumaCore.Api.Features.Auth;

/// <summary>
/// An <see cref="IOpenApiOperationTransformer"/> that adds <c>Set-Cookie</c> response headers to the OpenAPI
/// specification for endpoints annotated with <see cref="SetCookieHeaderMetadata"/>.
/// </summary>
/// <remarks>
/// This transformer inspects endpoint metadata for <see cref="SetCookieHeaderMetadata"/> instances and, for each
/// match, adds a <c>Set-Cookie</c> header definition to the corresponding response status code. This is used by
/// the authentication endpoints to document cookie-based token transport.
/// </remarks>
sealed class SetCookieHeaderTransformer : IOpenApiOperationTransformer
{
	/// <summary>
	/// Transforms the <see cref="OpenApiOperation"/> by adding <c>Set-Cookie</c> response headers based on
	/// <see cref="SetCookieHeaderMetadata"/> in the endpoint metadata.
	/// </summary>
	/// <param name="operation">The <see cref="OpenApiOperation"/> to transform.</param>
	/// <param name="context">
	/// The <see cref="OpenApiOperationTransformerContext"/> containing endpoint metadata.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	public Task TransformAsync(
		OpenApiOperation                   operation,
		OpenApiOperationTransformerContext context,
		CancellationToken                  cancellationToken)
	{
		IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

		foreach (SetCookieHeaderMetadata setCookie in metadata.OfType<SetCookieHeaderMetadata>())
		{
			if (operation.Responses?.TryGetValue(setCookie.StatusCode, out IOpenApiResponse? response) is true &&
			    response is OpenApiResponse concreteResponse)
			{
				concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
				concreteResponse.Headers["Set-Cookie"] = new OpenApiHeader
				{
					Description = setCookie.Description,
					Schema = new OpenApiSchema { Type = JsonSchemaType.String }
				};
			}
		}

		return Task.CompletedTask;
	}
}
