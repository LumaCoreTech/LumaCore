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

namespace LumaCore.Api.Tests.Features.Auth;

public sealed partial class TokenRevocationServiceTests
{
	/// <summary>
	/// Encapsulates all test dependencies for <see cref="TokenRevocationService"/>: a SQLite in-memory
	/// <see cref="LumaCoreDbContext"/>, a real <see cref="MemoryCache"/>, a <see cref="FakeTimeProvider"/>,
	/// and configurable <see cref="TokenRevocationOptions"/>.
	/// </summary>
	/// <remarks>
	///     <para>
	///     The SQLite connection is kept open for the lifetime of the harness. Call <see cref="DisposeAsync"/>
	///     to clean up all resources. Do not use <c>await using</c> — per project conventions, use
	///     <c>try/finally</c> with explicit <see cref="DisposeAsync"/> instead.
	///     </para>
	///     <para>
	///     Multiple harnesses can share the same in-memory database by passing the first harness's
	///     <see cref="ConnectionString"/> to the second harness's constructor. This enables cross-instance
	///     test scenarios such as concurrent revocation.
	///     </para>
	/// </remarks>
	private sealed class TestHarness : IAsyncDisposable
	{
		private readonly SqliteConnection mConnection;
		private readonly MemoryCache      mCache;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestHarness"/> class with an isolated in-memory database.
		/// </summary>
		/// <param name="cacheDurationSeconds">
		/// The cache duration in seconds for negative lookup results. Defaults to <c>5</c>.
		/// </param>
		public TestHarness(int cacheDurationSeconds = 5)
			: this(
				$"Data Source=Test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
				cacheDurationSeconds,
				ensureCreated: true) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="TestHarness"/> class that connects to an existing
		/// shared in-memory database identified by <paramref name="connectionString"/>. The database schema
		/// must already exist (created by a prior harness using the default constructor).
		/// </summary>
		/// <param name="connectionString">
		/// The SQLite connection string of the shared database to connect to.
		/// </param>
		/// <param name="cacheDurationSeconds">
		/// The cache duration in seconds for negative lookup results. Defaults to <c>5</c>.
		/// </param>
		public TestHarness(string connectionString, int cacheDurationSeconds = 5)
			: this(connectionString, cacheDurationSeconds, ensureCreated: false) { }

		/// <summary>
		/// Shared initialization logic for both public constructors.
		/// </summary>
		/// <param name="connectionString">The SQLite connection string.</param>
		/// <param name="cacheDurationSeconds">The cache duration in seconds for negative lookup results.</param>
		/// <param name="ensureCreated">
		/// <see langword="true"/> to create the database schema; <see langword="false"/> when connecting to
		/// an existing database.
		/// </param>
		private TestHarness(string connectionString, int cacheDurationSeconds, bool ensureCreated)
		{
			ConnectionString = connectionString;
			mConnection = new SqliteConnection(connectionString);
			mConnection.Open();

			DbContextOptions<LumaCoreDbContext> dbOptions = new DbContextOptionsBuilder<LumaCoreDbContext>()
				.UseSqlite(mConnection)
				.Options;

			DbContext = new LumaCoreDbContext(dbOptions);
			if (ensureCreated)
				DbContext.Database.EnsureCreated();

			mCache = new MemoryCache(new MemoryCacheOptions());
			TimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

			IOptions<TokenRevocationOptions> revocationOptions = Options.Create(
				new TokenRevocationOptions
				{
					CacheDurationSeconds = cacheDurationSeconds
				});

			Service = new TokenRevocationService(DbContext, mCache, TimeProvider, revocationOptions);
		}

		/// <summary>
		/// Gets the SQLite connection string used by this harness. Can be passed to another
		/// <see cref="TestHarness"/> to share the same in-memory database.
		/// </summary>
		public string ConnectionString { get; }

		/// <summary>
		/// Gets the EF Core database context used by the service under test.
		/// </summary>
		public LumaCoreDbContext DbContext { get; }

		/// <summary>
		/// Gets the fake time provider for controlling revocation timestamps.
		/// </summary>
		public FakeTimeProvider TimeProvider { get; }

		/// <summary>
		/// Gets the <see cref="TokenRevocationService"/> instance under test.
		/// </summary>
		public TokenRevocationService Service { get; }

		/// <summary>
		/// Checks whether a cache entry exists for the given <paramref name="jti"/> in the negative-result cache.
		/// </summary>
		/// <param name="jti">The JWT ID to check.</param>
		/// <returns>
		/// <see langword="true"/> if a "not revoked" cache entry exists; otherwise, <see langword="false"/>.
		/// </returns>
		public bool HasCacheEntry(string jti) => mCache.TryGetValue($"token:notrevoked:{jti}", out object? _);

		/// <summary>
		/// Releases the database context, memory cache, and SQLite connection.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			await DbContext.DisposeAsync().ConfigureAwait(false);
			mCache.Dispose();
			await mConnection.DisposeAsync().ConfigureAwait(false);
		}
	}
}
