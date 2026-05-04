// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Security;

using Microsoft.Extensions.Options;

namespace LumaCore.Data.Services;

/// <summary>
/// Default implementation of <see cref="ILumaCoreDataService"/>.
/// </summary>
public sealed partial class LumaCoreDataService : ILumaCoreDataService
{
	private readonly DatabaseOptions   mDatabaseOptions;
	private readonly LumaCoreDbContext mDbContext;
	private readonly IResourceService  mResourceService;
	private readonly ISecretProtector  mSecretProtector;
	private readonly TimeProvider      mTimeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="LumaCoreDataService"/> class.
	/// </summary>
	/// <param name="dbContext">The EF Core database context.</param>
	/// <param name="databaseOptions">The database configuration options.</param>
	/// <param name="resourceService">The resource service for avatar storage via the resource system.</param>
	/// <param name="secretProtector">
	/// The secret protector for encrypting/decrypting sensitive data before storing it in the database.
	/// </param>
	/// <param name="timeProvider">The time provider for obtaining the current UTC time.</param>
	/// <exception cref="ArgumentNullException">
	/// Any of <paramref name="dbContext"/>, <paramref name="databaseOptions"/>, <paramref name="resourceService"/>,
	/// <paramref name="secretProtector"/>, or <paramref name="timeProvider"/> is <see langword="null"/>.
	/// </exception>
	public LumaCoreDataService(
		LumaCoreDbContext         dbContext,
		IOptions<DatabaseOptions> databaseOptions,
		IResourceService          resourceService,
		ISecretProtector          secretProtector,
		TimeProvider              timeProvider)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		ArgumentNullException.ThrowIfNull(databaseOptions);
		ArgumentNullException.ThrowIfNull(resourceService);
		ArgumentNullException.ThrowIfNull(secretProtector);
		ArgumentNullException.ThrowIfNull(timeProvider);
		mDbContext = dbContext;
		mDatabaseOptions = databaseOptions.Value;
		mResourceService = resourceService;
		mSecretProtector = secretProtector;
		mTimeProvider = timeProvider;
	}

	private bool PreferCompiledHotPathQueries => mDatabaseOptions.PreferCompiledHotPathQueries;

	/// <summary>
	/// Resolves the effective UTC timestamp for a mutation: returns the caller-supplied <paramref name="utcNow"/>
	/// when non-<see langword="null"/>, otherwise pulls a fresh value from the injected <see cref="TimeProvider"/>.
	/// </summary>
	/// <param name="utcNow">
	/// The caller-supplied timestamp, or <see langword="null"/> to use the configured
	/// <see cref="TimeProvider"/>.
	/// </param>
	/// <returns>A non-null UTC <see cref="DateTime"/> to use for the mutation.</returns>
	/// <remarks>
	/// Public mutation methods accept a nullable <c>utcNow</c> so callers that orchestrate multi-step operations can
	/// thread a single deterministic timestamp through the data layer, while ad-hoc callers (and tests) can omit it
	/// and rely on the injected clock.
	/// </remarks>
	private DateTime ResolveUtcNow(DateTime? utcNow) => utcNow ?? mTimeProvider.GetUtcNow().UtcDateTime;

	/// <summary>
	/// Materializes an <see cref="IAsyncEnumerable{T}"/> (typically the result of an EF Core compiled query)
	/// into a <see cref="List{T}"/> while honouring a <see cref="CancellationToken"/>.
	/// </summary>
	/// <typeparam name="T">The element type produced by the source sequence.</typeparam>
	/// <param name="source">The source sequence to enumerate.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>A list containing every element produced by <paramref name="source"/> in iteration order.</returns>
	/// <remarks>
	/// Compiled queries do not accept a <see cref="CancellationToken"/> directly; this helper bridges that
	/// gap by attaching the token to the async enumeration. Cancellation is therefore best-effort: in-flight
	/// rows already produced by the database are still appended before the loop observes the request.
	/// </remarks>
	private static async Task<List<T>> MaterializeAsync<T>(
		IAsyncEnumerable<T> source,
		CancellationToken   cancellationToken)
	{
		var result = new List<T>();
		await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
		{
			result.Add(item);
		}

		return result;
	}
}
