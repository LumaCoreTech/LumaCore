// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Core;
using LumaCore.Data.Entities;
using LumaCore.Definitions;

using Microsoft.EntityFrameworkCore;

namespace LumaCore.Data.Services;

public sealed partial class LumaCoreDataService
{
	#region Read APIs

	/// <inheritdoc/>
	public Task<ModelEndpointEntity?> GetModelEndpointByIdAsync(
		ModelEndpointId   endpointId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endpointId.Value);

		return mDbContext.ModelEndpoints
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.Id == endpointId, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<ModelEndpointEntity?> GetModelEndpointByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		return mDbContext.ModelEndpoints
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.PublicId == publicId, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<ModelEndpointEntity>> ListModelEndpointsAsync(
		bool              includeInactive,
		CancellationToken cancellationToken = default)
	{
		IQueryable<ModelEndpointEntity> query = mDbContext.ModelEndpoints.AsNoTracking();

		// Unless explicitly requested, hide deactivated endpoints from consumers.
		if (!includeInactive)
			query = query.Where(e => e.IsActive);

		return await query
			       .OrderBy(e => e.Name)
			       .ToListAsync(cancellationToken)
			       .ConfigureAwait(false);
	}

	#endregion

	#region Projection APIs

	/// <inheritdoc/>
	public async Task<string?> GetModelEndpointCredentialsAsync(
		ModelEndpointId   endpointId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endpointId.Value);

		// Only project the credentials column to avoid loading the full entity.
		string? encrypted = await mDbContext.ModelEndpoints
			                    .AsNoTracking()
			                    .Where(e => e.Id == endpointId)
			                    .Select(e => e.EncryptedCredentials)
			                    .FirstOrDefaultAsync(cancellationToken)
			                    .ConfigureAwait(false);

		if (string.IsNullOrEmpty(encrypted))
			return null;

		// Decrypt on read — credentials are always stored encrypted at rest.
		return mSecretProtector.Unprotect(encrypted);
	}

	#endregion

	#region Existence Checks

	/// <inheritdoc/>
	public Task<bool> ModelEndpointExistsAsync(
		ModelEndpointId   endpointId,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endpointId.Value);

		return mDbContext.ModelEndpoints
			.AsNoTracking()
			.AnyAsync(e => e.Id == endpointId, cancellationToken);
	}

	/// <inheritdoc/>
	public Task<bool> ModelEndpointExistsByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		return mDbContext.ModelEndpoints
			.AsNoTracking()
			.AnyAsync(e => e.PublicId == publicId, cancellationToken);
	}

	#endregion

	#region Mutation APIs

	/// <inheritdoc/>
	public async Task<ModelEndpointEntity> CreateModelEndpointAsync(
		Guid              publicId,
		string            providerType,
		string            baseUrl,
		string            name,
		string?           description,
		string?           credentials,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		Guard.ThrowIfEmpty(publicId);

		Guard.ThrowIfNullOrEmptyOrTooLong(
			providerType,
			EntityLimits.ModelEndpointProviderTypeMaxLength,
			out providerType);
		Guard.ThrowIfNullOrEmptyOrTooLong(baseUrl, EntityLimits.ModelEndpointBaseUrlMaxLength, out baseUrl);
		Guard.ThrowIfNullOrEmptyOrTooLong(name, EntityLimits.ModelEndpointNameMaxLength, out name);
		Guard.ThrowIfTooLong(description, EntityLimits.ModelEndpointDescriptionMaxLength, out description);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		var entity = new ModelEndpointEntity
		{
			PublicId = publicId,
			ProviderType = providerType,
			BaseUrl = baseUrl,
			Name = name,
			Description = description,
			IsActive = true, // New endpoints are active by default.
			CreatedAtUtc = effectiveUtcNow,
			UpdatedAtUtc = effectiveUtcNow,
			// Credentials are encrypted at rest; null means no credentials were provided.
			EncryptedCredentials = string.IsNullOrEmpty(credentials)
				                       ? null
				                       : mSecretProtector.Protect(credentials)
		};

		mDbContext.ModelEndpoints.Add(entity);
		await mDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		// Detach so the freshly inserted entity leaves the service untracked. Convention: tracked entities
		// must never cross the data-service boundary — callers must go through dedicated mutation methods
		// for any subsequent change, not by mutating this returned instance.
		mDbContext.Entry(entity).State = EntityState.Detached;
		return entity;
	}

	/// <inheritdoc/>
	public async Task<bool> UpdateModelEndpointAsync(
		ModelEndpointId   endpointId,
		string            name,
		string?           description,
		bool              isActive,
		string?           credentials,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endpointId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(name, EntityLimits.ModelEndpointNameMaxLength, out name);
		Guard.ThrowIfTooLong(description, EntityLimits.ModelEndpointDescriptionMaxLength, out description);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		// Encrypt before persisting; passing null clears the stored credentials.
		string? encrypted = string.IsNullOrEmpty(credentials)
			                    ? null
			                    : mSecretProtector.Protect(credentials);

		int updated = await mDbContext.ModelEndpoints
			              .Where(e => e.Id == endpointId)
			              .ExecuteUpdateAsync(
				              setters => setters
					              .SetProperty(e => e.Name, name)
					              .SetProperty(e => e.Description, description)
					              .SetProperty(e => e.IsActive, isActive)
					              .SetProperty(e => e.EncryptedCredentials, encrypted)
					              .SetProperty(e => e.UpdatedAtUtc, effectiveUtcNow),
				              cancellationToken)
			              .ConfigureAwait(false);

		return updated > 0;
	}

	/// <inheritdoc/>
	public async Task<bool> UpdateModelEndpointCredentialsAsync(
		ModelEndpointId   endpointId,
		string?           credentials,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endpointId.Value);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		string? encrypted = string.IsNullOrEmpty(credentials)
			                    ? null
			                    : mSecretProtector.Protect(credentials);

		int updated = await mDbContext.ModelEndpoints
			              .Where(e => e.Id == endpointId)
			              .ExecuteUpdateAsync(
				              setters => setters
					              .SetProperty(e => e.EncryptedCredentials, encrypted)
					              .SetProperty(e => e.UpdatedAtUtc, effectiveUtcNow),
				              cancellationToken)
			              .ConfigureAwait(false);

		return updated > 0;
	}

	/// <inheritdoc/>
	public async Task<bool> UpdateModelEndpointMetadataAsync(
		ModelEndpointId   endpointId,
		string            name,
		string?           description,
		bool              isActive,
		DateTime?         utcNow            = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endpointId.Value);
		Guard.ThrowIfNullOrEmptyOrTooLong(name, EntityLimits.ModelEndpointNameMaxLength, out name);
		Guard.ThrowIfTooLong(description, EntityLimits.ModelEndpointDescriptionMaxLength, out description);

		DateTime effectiveUtcNow = ResolveUtcNow(utcNow);

		int updated = await mDbContext.ModelEndpoints
			              .Where(e => e.Id == endpointId)
			              .ExecuteUpdateAsync(
				              setters => setters
					              .SetProperty(e => e.Name, name)
					              .SetProperty(e => e.Description, description)
					              .SetProperty(e => e.IsActive, isActive)
					              .SetProperty(e => e.UpdatedAtUtc, effectiveUtcNow),
				              cancellationToken)
			              .ConfigureAwait(false);

		return updated > 0;
	}

	#endregion
}
