// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Api.Features.Auth;
using LumaCore.Data;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class TokenRevocationServiceTests
{
	/// <summary>
	/// Verifies that the constructor succeeds when all parameters are valid.
	/// </summary>
	[Fact]
	public async Task Constructor_WhenAllParametersValid_CreatesInstance()
	{
		// Arrange + Act
		var harness = new TestHarness();

		try
		{
			// Assert
			Assert.NotNull(harness.Service);
		}
		finally
		{
			await harness.DisposeAsync();
		}
	}

	/// <summary>
	/// Test data for <see cref="Constructor_WhenRequiredParameterIsNull_ThrowsArgumentNullException"/>:
	/// one row per constructor parameter that has a <see langword="null"/> guard.
	/// </summary>
	public static TheoryData<string> Constructor_NullArguments_Data() => new()
	{
		// Each row: the parameter name that is passed as null.
		"dbContext",
		"cache",
		"timeProvider",
		"options"
	};

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
	/// <paramref name="paramName"/> identifies a <see langword="null"/> argument.
	/// </summary>
	/// <param name="paramName">The name of the parameter that is <see langword="null"/>.</param>
	[Theory]
	[MemberData(nameof(Constructor_NullArguments_Data))]
	public void Constructor_WhenRequiredParameterIsNull_ThrowsArgumentNullException(string paramName)
	{
		// Arrange — create all real dependencies first, then selectively null out the one
		// identified by paramName. This lets a single Theory cover every null guard without
		// duplicating the entire setup for each parameter.
		using var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		DbContextOptions<LumaCoreDbContext> dbOptions =
			new DbContextOptionsBuilder<LumaCoreDbContext>().UseSqlite(connection).Options;
		var dbContext = new LumaCoreDbContext(dbOptions);
		var cache = new MemoryCache(new MemoryCacheOptions());
		var timeProvider = new FakeTimeProvider();
		IOptions<TokenRevocationOptions> options = Options.Create(new TokenRevocationOptions());

		// Replace exactly one argument with null — the one matching paramName.
		LumaCoreDbContext? argDbContext = paramName == "dbContext" ? null : dbContext;
		IMemoryCache? argCache = paramName == "cache" ? null : cache;
		TimeProvider? argTimeProvider = paramName == "timeProvider" ? null : timeProvider;
		IOptions<TokenRevocationOptions>? argOptions = paramName == "options" ? null : options;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new TokenRevocationService(
			argDbContext!,
			argCache!,
			argTimeProvider!,
			argOptions!));
		Assert.Equal(paramName, ex.ParamName);

		// Cleanup — dispose resources not consumed by the (failed) constructor.
		cache.Dispose();
		dbContext.Dispose();
	}
}
