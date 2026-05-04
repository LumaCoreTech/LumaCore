// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Queries;
using LumaCore.Data.Services;

using Xunit;

namespace LumaCore.Data.Tests.Queries;

public sealed partial class CompiledQueryRegressionTests
{
	/// <summary>
	/// Verifies that <see cref="ResourceQueries.GetActiveByContentHash"/> returns the seeded
	/// <see cref="ResourceDeletionState.Active"/> resource for a matching hash and <see langword="null"/>
	/// for an unknown hash.
	/// </summary>
	/// <param name="contentHashSelector">
	/// Selector indicating whether to probe with the seeded hash (<c>"seeded"</c>) or an unknown hash
	/// (<c>"missing"</c>). Indirection via a selector keeps the seeded hash out of the
	/// <see cref="InlineDataAttribute"/> arguments, which must be compile-time constants.
	/// </param>
	/// <param name="expectMatch">Whether the lookup is expected to find a row.</param>
	[Theory]
	[InlineData("seeded", true)]   // Seeded Active resource.
	[InlineData("missing", false)] // No row with this hash.
	public async Task ResourceQueries_GetActiveByContentHash_ReturnsActiveResourceOnlyForSeededHash(
		string contentHashSelector,
		bool   expectMatch)
	{
		// Arrange
		string contentHash = contentHashSelector == "seeded"
			                     ? mResourceContentHash
			                     : new string('b', 64);

		// Act
		ResourceEntity? result = await ResourceQueries.GetActiveByContentHash(mFixture.DbContext, contentHash);

		// Assert
		if (expectMatch)
		{
			Assert.NotNull(result);
			Assert.Equal(mResourceId, result.Id);
			Assert.Equal(mResourceContentHash, result.ContentHash);
			Assert.Equal(ResourceDeletionState.Active, result.DeletionState);
		}
		else
		{
			Assert.Null(result);
		}
	}

	/// <summary>
	/// Verifies that <see cref="ResourceQueries.GetDeletionStateById"/> returns
	/// <see cref="ResourceDeletionState.Active"/> for the seeded resource.
	/// </summary>
	[Fact]
	public async Task ResourceQueries_GetDeletionStateById_ReturnsActiveForSeededResource()
	{
		// Act
		ResourceDeletionState? result = await ResourceQueries.GetDeletionStateById(mFixture.DbContext, mResourceId);

		// Assert
		Assert.Equal(ResourceDeletionState.Active, result);
	}

	/// <summary>
	/// Verifies that <see cref="ResourceQueries.GetDownloadInfoByPublicId"/> joins
	/// <see cref="ResourceReferenceEntity"/> with <see cref="ResourceEntity"/> and returns the full
	/// <see cref="ResourceDownloadInfo"/> projection in a single round-trip.
	/// </summary>
	/// <remarks>
	/// All four projection fields are asserted individually — this test is the model-compatibility guard
	/// for the join shape. If the underlying configuration of either entity drifts (column rename,
	/// relationship rewire, value-converter change), the projection breaks and this test fails at the
	/// first <c>EF.CompileAsyncQuery</c> execution rather than silently in production.
	/// </remarks>
	[Fact]
	public async Task ResourceQueries_GetDownloadInfoByPublicId_ReturnsCompleteDownloadMetadata()
	{
		// Act
		ResourceDownloadInfo? result =
			await ResourceQueries.GetDownloadInfoByPublicId(mFixture.DbContext, mResourceReferencePublicId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal("ab/abcdef01-2345-6789-abcd-ef0123456789", result.StoragePath);
		Assert.Equal("image/png", result.ContentType);
		Assert.Equal("test.png", result.OriginalFileName);
		Assert.Equal(1024L, result.SizeBytes);
	}
}
