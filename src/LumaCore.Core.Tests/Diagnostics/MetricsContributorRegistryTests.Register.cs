// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core.Diagnostics;

using Xunit;

namespace LumaCore.Core.Tests.Diagnostics;

/// <summary>
/// Tests for <see cref="MetricsContributorRegistry.Register(string, Type)"/> and the generic
/// <see cref="MetricsContributorRegistry.Register{TContributor}(string)"/> overload.
/// </summary>
/// <remarks>
/// Test ordering follows the validation flow inside <see cref="MetricsContributorRegistry.Register(string, Type)"/>:
/// <list type="number">
///     <item>Valid registration (happy path).</item>
///     <item>Argument violations (null type, null/whitespace section name).</item>
///     <item>Domain violations (reserved names, underscore-prefixed names).</item>
///     <item>State violations (already registered — exact match and case-insensitive duplicate).</item>
/// </list>
/// The generic overload test sits at the end to keep the non-generic flow uninterrupted.
/// </remarks>
public sealed partial class MetricsContributorRegistryTests
{
	#region Register(string, Type)

	/// <summary>
	/// Verifies that a normal contributor with a unique, non-reserved section name registers successfully and
	/// shows up in <see cref="MetricsContributorRegistry.Descriptors"/>.
	/// </summary>
	[Fact]
	public void Register_WhenSectionNameValid_RegistersDescriptor()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();

		// Act
		registry.Register("featureA", typeof(SampleContributor));

		// Assert
		MetricsContributorDescriptor descriptor = Assert.Single(registry.Descriptors);
		Assert.Equal("featureA", descriptor.SectionName);
		Assert.Equal(typeof(SampleContributor), descriptor.ImplementationType);
	}

	/// <summary>
	/// Verifies that a null <c>implementationType</c> is rejected with <see cref="ArgumentNullException"/>
	/// and the offending parameter name is reported.
	/// </summary>
	[Fact]
	public void Register_WhenImplementationTypeIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => registry.Register("featureA", implementationType: null!));
		Assert.Equal("implementationType", ex.ParamName);
	}

	/// <summary>
	/// Verifies that null/empty/whitespace section names are rejected with <see cref="ArgumentException"/>,
	/// the parameter name is reported, and the custom message describes the rejection reason.
	/// </summary>
	/// <param name="sectionName">The invalid section name to test.</param>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t")]
	public void Register_WhenSectionNameIsNullOrWhitespace_ThrowsArgumentException(string? sectionName)
	{
		// Arrange
		var registry = new MetricsContributorRegistry();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => registry.Register(sectionName!, typeof(SampleContributor)));
		Assert.Equal("sectionName", ex.ParamName);
		// Assert only the production-controlled message body. ArgumentException.Message appends a
		// "(Parameter '...')" suffix from a localized BCL resource, which differs on non-English systems.
		Assert.StartsWith(
			"Metrics contributor 'SampleContributor' has an invalid section name. "
			+ "Section name cannot be null, empty, or whitespace.",
			ex.Message,
			StringComparison.Ordinal);
		Assert.Empty(registry.Descriptors);
	}

	/// <summary>
	/// Verifies that all reserved section names are rejected with <see cref="ArgumentException"/>,
	/// the parameter name is reported, and the custom message names the reserved section.
	/// </summary>
	/// <param name="sectionName">A reserved section name (case variants included).</param>
	[Theory]
	[InlineData("timestamp")]
	[InlineData("Timestamp")]
	[InlineData("gc")]
	[InlineData("GC")]
	[InlineData("memory")]
	[InlineData("process")]
	[InlineData("threadPool")]
	[InlineData("THREADPOOL")]
	[InlineData("_errors")]
	public void Register_WhenSectionNameIsReserved_ThrowsArgumentException(string sectionName)
	{
		// Arrange
		var registry = new MetricsContributorRegistry();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => registry.Register(sectionName, typeof(SampleContributor)));
		Assert.Equal("sectionName", ex.ParamName);
		// Assert only the production-controlled message body. ArgumentException.Message appends a
		// "(Parameter '...')" suffix from a localized BCL resource, which differs on non-English systems.
		Assert.StartsWith(
			$"Metrics contributor 'SampleContributor' cannot use section name '{sectionName}' "
			+ "because it is reserved for internal use.",
			ex.Message,
			StringComparison.Ordinal);
		Assert.Empty(registry.Descriptors);
	}

	/// <summary>
	/// Verifies that section names beginning with an underscore (other than the explicitly reserved ones) are
	/// rejected as future meta-section reservations and the parameter name is reported.
	/// </summary>
	/// <param name="sectionName">An underscore-prefixed section name.</param>
	[Theory]
	[InlineData("_custom")]
	[InlineData("_meta")]
	[InlineData("_anything")]
	public void Register_WhenSectionNameStartsWithUnderscore_ThrowsArgumentException(string sectionName)
	{
		// Arrange
		var registry = new MetricsContributorRegistry();

		// Act + Assert
		var ex = Assert.Throws<ArgumentException>(() => registry.Register(sectionName, typeof(SampleContributor)));
		Assert.Equal("sectionName", ex.ParamName);
		// Assert only the production-controlled message body. ArgumentException.Message appends a
		// "(Parameter '...')" suffix from a localized BCL resource, which differs on non-English systems.
		Assert.StartsWith(
			$"Metrics contributor 'SampleContributor' cannot use section name '{sectionName}' "
			+ "because names starting with '_' are reserved for meta-sections.",
			ex.Message,
			StringComparison.Ordinal);
		Assert.Empty(registry.Descriptors);
	}

	/// <summary>
	/// Verifies that registering the same section name twice raises <see cref="InvalidOperationException"/>
	/// with a message that names both the new and the already-registered contributor type.
	/// </summary>
	[Fact]
	public void Register_WhenSectionNameAlreadyRegistered_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();
		registry.Register("featureA", typeof(SampleContributor));

		// Act + Assert
		var ex =
			Assert.Throws<InvalidOperationException>(() => registry.Register("featureA", typeof(SecondContributor)));
		Assert.Equal(
			"Metrics contributor 'SecondContributor' cannot use section name 'featureA' " +
			"because it is already registered by 'SampleContributor'.",
			ex.Message);

		// Failed registration must not have mutated the registry.
		MetricsContributorDescriptor descriptor = Assert.Single(registry.Descriptors);
		Assert.Equal(new MetricsContributorDescriptor("featureA", typeof(SampleContributor)), descriptor);
	}

	/// <summary>
	/// Verifies that registration is case-insensitive: a name that differs only by case from a
	/// previously registered name is rejected as a duplicate.
	/// </summary>
	[Fact]
	public void Register_WhenSectionNameDiffersOnlyByCase_ThrowsInvalidOperationException()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();
		registry.Register("featureA", typeof(SampleContributor));

		// Act + Assert
		// Note: the existing key was stored as "featureA"; the registry reuses that exact spelling in the
		// error message, even though the new call passes "FEATUREA".
		var ex =
			Assert.Throws<InvalidOperationException>(() => registry.Register("FEATUREA", typeof(SecondContributor)));
		Assert.Equal(
			"Metrics contributor 'SecondContributor' cannot use section name 'FEATUREA' " +
			"because it is already registered by 'SampleContributor'.",
			ex.Message);

		// Failed registration must not have mutated the registry.
		MetricsContributorDescriptor descriptor = Assert.Single(registry.Descriptors);
		Assert.Equal(new MetricsContributorDescriptor("featureA", typeof(SampleContributor)), descriptor);
	}

	#endregion

	#region Register<TContributor>(string)

	/// <summary>
	/// Verifies that the generic <see cref="MetricsContributorRegistry.Register{TContributor}(string)"/>
	/// overload registers the contributor under the requested section name.
	/// </summary>
	[Fact]
	public void RegisterGeneric_WhenSectionNameValid_RegistersUnderRequestedSection()
	{
		// Arrange
		var registry = new MetricsContributorRegistry();

		// Act
		registry.Register<SampleContributor>("featureA");

		// Assert
		MetricsContributorDescriptor descriptor = Assert.Single(registry.Descriptors);
		Assert.Equal("featureA", descriptor.SectionName);
		Assert.Equal(typeof(SampleContributor), descriptor.ImplementationType);
	}

	#endregion
}
