// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.Validation;

namespace LumaCore.Api.Tests.Features.Validation;

// ReSharper disable ClassNeverInstantiated.Local
public sealed partial class ValidationFilterTests
{
	/// <summary>
	/// A minimal request model with a single required field, used to exercise the
	/// <see cref="ValidationFilter"/> for basic pass/fail scenarios.
	/// </summary>
	/// <param name="Name">A required field. Validation rejects requests where this is <see langword="null"/>.</param>
	private sealed record ValidatedProbeRequest(
		[property: Required(ErrorMessage = "Name is required.")]
		string Name);

	/// <summary>
	/// A request model with multiple required fields, used to verify that the <see cref="ValidationFilter"/>
	/// collects errors from <b>all</b> invalid fields — not just the first one encountered.
	/// </summary>
	/// <param name="Name">A required field.</param>
	/// <param name="Email">A required field.</param>
	private sealed record MultiFieldRequest(
		[property: Required(ErrorMessage = "Name is required.")]
		string Name,
		[property: Required(ErrorMessage = "Email is required.")]
		string Email);
}
