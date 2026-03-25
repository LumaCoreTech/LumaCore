// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.ComponentModel.DataAnnotations;

using LumaCore.Api.Features.ApiVersioning;
using LumaCore.Api.Features.Validation;

// ReSharper disable ClassNeverInstantiated.Local

namespace LumaCore.Api.Tests.Features.ApiVersioning;

public sealed partial class VersionedApiGroupIntegrationTests
{
	/// <summary>
	/// A minimal request model with validation attributes, used as a probe to exercise the
	/// <see cref="ValidationFilter"/> registered by <see cref="VersionedApiGroup.MapVersionedApiGroup"/>
	/// via <c>WithValidation()</c>.
	/// </summary>
	/// <param name="Name">A required field. Validation rejects requests where this is <see langword="null"/>.</param>
	private sealed record ValidatedProbeRequest(
		[property: Required(ErrorMessage = "Name is required.")]
		string Name);
}
