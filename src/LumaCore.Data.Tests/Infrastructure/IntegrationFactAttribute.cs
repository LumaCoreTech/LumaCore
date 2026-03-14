// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Marks a test as an integration test that requires external infrastructure (e.g., Docker-hosted databases).
/// </summary>
/// <remarks>
///     <para>
///     Tests decorated with this attribute are automatically <b>skipped</b> unless the environment variable
///     <c>LUMACORE_INTEGRATION_TESTS</c> is set to a non-empty value. This prevents integration tests from
///     failing during normal "Run All Tests" in Visual Studio or ReSharper — they show as <b>skipped</b> (yellow)
///     instead of failed (red).
///     </para>
///     <para>
///         <b>Usage:</b>
///     </para>
///     <list type="bullet">
///         <item><b>Local development:</b> Integration tests are skipped by default — no configuration needed.</item>
///         <item>
///         <b>CI / Docker:</b> Set <c>LUMACORE_INTEGRATION_TESTS=true</c> in the environment to enable them.
///         </item>
///         <item>
///         <b>CLI opt-in:</b> <c>dotnet test --filter "Category=Integration"</c> runs only integration tests
///         (still requires the env var).
///         </item>
///     </list>
///     <para>
///     Tests using this attribute should also include <c>[Trait("Category", "Integration")]</c> on the test class
///     for CLI-based filtering.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// [IntegrationFact]
/// public async Task SomeTest_RequiringExternalDatabase()
/// {
///     // This test only runs when LUMACORE_INTEGRATION_TESTS is set.
/// }
/// </code>
/// </example>
public sealed class IntegrationFactAttribute : FactAttribute
{
	/// <summary>
	/// The environment variable that must be set to enable integration tests.
	/// </summary>
	private const string EnvironmentVariable = "LUMACORE_INTEGRATION_TESTS";

	/// <summary>
	/// Initializes a new instance of the <see cref="IntegrationFactAttribute"/> class.
	/// If the <c>LUMACORE_INTEGRATION_TESTS</c> environment variable is not set, the test is automatically skipped.
	/// </summary>
	public IntegrationFactAttribute()
	{
		if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariable)))
		{
			Skip = $"Integration tests are disabled. Set {EnvironmentVariable}=true to enable.";
		}
	}
}
