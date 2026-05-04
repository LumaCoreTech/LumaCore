// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Security;
using LumaCore.Data.Services;

using Microsoft.Extensions.Options;

namespace LumaCore.Data.Tests.Infrastructure;

/// <summary>
/// Factory for creating <see cref="LumaCoreDataService"/> instances with sensible defaults for test scenarios.
/// </summary>
static class LumaCoreDataServiceFactory
{
	/// <summary>
	/// Creates a new <see cref="LumaCoreDataService"/> backed by the specified <paramref name="dbContext"/> with
	/// default <see cref="DatabaseOptions"/> and a test-only encryption key.
	/// </summary>
	/// <param name="dbContext">The EF Core context to use for data access.</param>
	/// <param name="configure">
	/// An optional callback to override specific <see cref="DatabaseOptions"/> properties for targeted branch
	/// testing.
	/// </param>
	/// <param name="resourceService">
	/// An optional <see cref="IResourceService"/> override. Defaults to <see cref="NoOpResourceService"/>, which is
	/// sufficient for tests that do not exercise the resource subsystem. Tests that need to assert on resource
	/// behavior (e.g. avatar upload) should pass a real <see cref="ResourceService"/> backed by a fake store.
	/// </param>
	/// <param name="timeProvider">
	/// An optional <see cref="TimeProvider"/> override. Defaults to <see cref="TimeProvider.System"/>. Tests that
	/// need to verify the nullable-<c>utcNow</c> fallback behaviour should pass a deterministic clock (e.g.
	/// <c>FakeTimeProvider</c>).
	/// </param>
	/// <returns>A fully configured <see cref="LumaCoreDataService"/> instance ready for testing.</returns>
	public static LumaCoreDataService Create(
		LumaCoreDbContext        dbContext,
		Action<DatabaseOptions>? configure       = null,
		IResourceService?        resourceService = null,
		TimeProvider?            timeProvider    = null)
	{
		// Special test helper: most tests don't care about DatabaseOptions, so we start from defaults.
		// The optional configure callback allows targeted branch testing for option-driven behavior.
		var options = new DatabaseOptions
		{
			EncryptionKey = "DEV-ONLY-CHANGE-THIS-TO-A-LONG-RANDOM-SECRET-STRING"
		};
		configure?.Invoke(options);
		var protector = new AesGcmSecretProtector(Options.Create(options));
		return new LumaCoreDataService(
			dbContext,
			Options.Create(options),
			resourceService ?? new NoOpResourceService(),
			protector,
			timeProvider ?? TimeProvider.System);
	}

	/// <summary>
	/// Default <see cref="IResourceService"/> for tests that do not exercise the resource subsystem.
	/// </summary>
	/// <remarks>
	///     <para>
	///     Methods that would otherwise return sentinel values (<c>Guid.Empty</c>, <see langword="null"/>) throw
	///     <see cref="NotSupportedException"/> instead. Silently returning a sentinel makes a test that accidentally
	///     hits the resource pipeline appear to pass while producing structurally invalid data — exactly the kind of
	///     implicit operating assumption the repository's coding standards forbid.
	///     </para>
	///     <para>
	///     <see cref="DeleteReferencesByOwnerAsync"/> intentionally still returns <c>0</c>: for an empty resource
	///     store that is the structurally honest answer (no references existed, none were deleted), and several
	///     unrelated user-deletion tests legitimately rely on this no-op behaviour.
	///     </para>
	///     <para>
	///     Tests that need real resource semantics must wire a <see cref="ResourceService"/> backed by
	///     <c>FakeResourceStore</c> (see <c>CreateServiceWithRealResources</c>).
	///     </para>
	/// </remarks>
	internal sealed class NoOpResourceService : IResourceService
	{
		/// <inheritdoc/>
		/// <exception cref="NotSupportedException">
		/// Always — the no-op default cannot fabricate a meaningful upload result. Tests that need this method must
		/// inject a real <see cref="ResourceService"/>.
		/// </exception>
		public Task<ResourceUploadResult> UploadAsync(
			Stream            content,
			ResourceOwnerKind ownerKind,
			ResourceOwnerId   ownerId,
			string            contentType,
			ParticipantId?    createdByParticipantId,
			DateTime?         utcNow            = null,
			string?           originalFileName  = null,
			CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException(
				$"{nameof(NoOpResourceService)}.{nameof(UploadAsync)} was called by a test that did not wire a real "
				+ "resource service. Use CreateServiceWithRealResources(...) (or pass an explicit IResourceService to "
				+ "LumaCoreDataServiceFactory.Create) for tests that exercise the resource pipeline.");
		}

		/// <inheritdoc/>
		/// <exception cref="NotSupportedException">
		/// Always — the no-op default cannot fabricate a meaningful download info. Tests that need this method must
		/// inject a real <see cref="ResourceService"/>.
		/// </exception>
		public Task<ResourceDownloadInfo?> GetDownloadInfoAsync(
			Guid              publicId,
			CancellationToken cancellationToken = default)
		{
			throw new NotSupportedException(
				$"{nameof(NoOpResourceService)}.{nameof(GetDownloadInfoAsync)} was called by a test that did not wire "
				+ "a real resource service. Use CreateServiceWithRealResources(...) (or pass an explicit "
				+ "IResourceService to LumaCoreDataServiceFactory.Create) for tests that exercise the resource "
				+ "pipeline.");
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Returns <c>0</c> unconditionally because the no-op service has no backing store: a structurally honest
		/// answer for tests (e.g. user-deletion tests) that don't seed any resource references but still trigger the
		/// owner-cascade cleanup.
		/// </remarks>
		public Task<int> DeleteReferencesByOwnerAsync(
			ResourceOwnerKind ownerKind,
			ResourceOwnerId   ownerId,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(0);
		}
	}
}
