// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.ErrorHandling;

using Xunit;

namespace LumaCore.Api.Tests.Features.ErrorHandling;

public sealed partial class StatusCodeMappingIntegrationTests
{
	/// <summary>
	/// Provides test data for all 18 known status code mappings defined in
	/// <see cref="MiddlewareIntegration.UseErrorHandlingFeature"/>. Each row contains the scenario label,
	/// HTTP status code, and the expected <see cref="ErrorTypes"/> URN, title, and detail from
	/// <c>MapStatusCodeToErrorInfo()</c>.
	/// </summary>
	public static TheoryData<string, int, string, string, string> KnownStatusCodeData => new()
	{
		// 4xx Client Errors
		{
			"400 Bad Request", 400, ErrorTypes.BadRequest,
			"Bad Request", "The request is malformed or contains invalid data."
		},
		{
			"401 Unauthorized", 401, ErrorTypes.Unauthorized,
			"Authentication Required",
			"Valid credentials are required to access this resource."
		},
		{
			"403 Forbidden", 403, ErrorTypes.Forbidden,
			"Access Denied", "Insufficient permissions to access this resource."
		},
		{
			"404 Not Found", 404, ErrorTypes.NotFound,
			"Resource Not Found", "The requested resource does not exist."
		},
		{
			"405 Method Not Allowed", 405, ErrorTypes.MethodNotAllowed,
			"Method Not Allowed",
			"The HTTP method is not supported for this endpoint."
		},
		{
			"406 Not Acceptable", 406, ErrorTypes.NotAcceptable,
			"Not Acceptable",
			"The server cannot produce a response matching the Accept header."
		},
		{
			"408 Request Timeout", 408, ErrorTypes.RequestTimeout,
			"Request Timeout",
			"The server timed out waiting for the request."
		},
		{
			"409 Conflict", 409, ErrorTypes.Conflict,
			"Conflict",
			"The request conflicts with the current state of the resource."
		},
		{
			"410 Gone", 410, ErrorTypes.Gone,
			"Gone", "The requested resource has been permanently removed."
		},
		{
			"413 Payload Too Large", 413, ErrorTypes.PayloadTooLarge,
			"Payload Too Large",
			"The request payload exceeds the server's size limit."
		},
		{
			"415 Unsupported Media Type", 415, ErrorTypes.UnsupportedMediaType,
			"Unsupported Media Type",
			"The request content type is not supported."
		},
		{
			"422 Unprocessable Entity", 422, ErrorTypes.Validation,
			"Validation Failed", "The request data failed validation."
		},
		{
			"426 Upgrade Required", 426, ErrorTypes.UpgradeRequired,
			"Upgrade Required",
			"The client must switch to a different protocol."
		},
		{
			"429 Too Many Requests", 429, ErrorTypes.RateLimited,
			"Rate Limit Exceeded",
			"Request rate limit exceeded. Retry after cooldown."
		},
		{
			"431 Headers Too Large", 431, ErrorTypes.HeadersTooLarge,
			"Request Header Fields Too Large",
			"The request headers exceed the server's size limit."
		},

		// 5xx Server Errors
		{
			"500 Internal Server Error", 500, ErrorTypes.Internal,
			"Internal Server Error", "An unexpected error occurred."
		},
		{
			"501 Not Implemented", 501, ErrorTypes.NotImplemented,
			"Not Implemented",
			"The requested functionality is not supported."
		},
		{
			"503 Service Unavailable", 503, ErrorTypes.ServiceUnavailable,
			"Service Unavailable",
			"The service is temporarily unavailable."
		},
	};
}
