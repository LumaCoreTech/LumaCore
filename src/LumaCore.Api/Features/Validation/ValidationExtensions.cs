// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Api.Features.Validation;

/// <summary>
/// Provides extension methods for applying validation filters to endpoints and route groups.
/// </summary>
/// <remarks>
///     <para>
///     These extension methods provide a fluent API for applying the <see cref="ValidationFilter"/>
///     to minimal API endpoints and route groups. The filter validates request parameters using
///     <see cref="System.ComponentModel.DataAnnotations"/> and returns RFC 7807 compliant
///     validation errors.
///     </para>
/// </remarks>
static class ValidationExtensions
{
	/// <summary>
	/// Adds automatic <see cref="System.ComponentModel.DataAnnotations"/> validation to the endpoint.
	/// </summary>
	/// <typeparam name="TBuilder">
	/// The type of the endpoint convention builder, must implement <see cref="IEndpointConventionBuilder"/>.
	/// </typeparam>
	/// <param name="builder">The endpoint builder to add validation to.</param>
	/// <returns>The <paramref name="builder"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     When applied, all request parameters with <see cref="System.ComponentModel.DataAnnotations"/>
	///     attributes (such as <c>[Required]</c>, <c>[Range]</c>, <c>[StringLength]</c>) are automatically
	///     validated. Invalid requests receive a <c>400 Bad Request</c> response with
	///     detailed validation errors in <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> format.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// app.MapPost("/items", (CreateItemRequest request) => ...)
	///    .WithValidation()
	///    .WithName("CreateItem");
	/// </code>
	/// </example>
	public static TBuilder WithValidation<TBuilder>(this TBuilder builder)
		where TBuilder : IEndpointConventionBuilder
	{
		builder.AddEndpointFilter(new ValidationFilter());
		return builder;
	}

	/// <summary>
	/// Adds automatic <see cref="System.ComponentModel.DataAnnotations"/> validation to all
	/// endpoints in the route group.
	/// </summary>
	/// <param name="group">The <see cref="RouteGroupBuilder"/> to add validation to.</param>
	/// <returns>The <paramref name="group"/> for method chaining.</returns>
	/// <remarks>
	///     <para>
	///     When applied to a <see cref="RouteGroupBuilder"/>, all endpoints within that group
	///     automatically validate their request parameters using
	///     <see cref="System.ComponentModel.DataAnnotations"/>. This is more efficient than
	///     adding validation to each endpoint individually.
	///     </para>
	/// </remarks>
	/// <example>
	///     <code>
	/// app.MapGroup("/api/v1/items")
	///    .WithValidation()
	///    .MapPost("/", HandleCreate)
	///    .MapPut("/{id}", HandleUpdate);
	/// </code>
	/// </example>
	public static RouteGroupBuilder WithValidation(this RouteGroupBuilder group)
	{
		group.AddEndpointFilter(new ValidationFilter());
		return group;
	}
}
