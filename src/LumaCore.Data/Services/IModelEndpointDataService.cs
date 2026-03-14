// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Security.Cryptography;

using LumaCore.Data.Entities;

namespace LumaCore.Data.Services;

/// <summary>
/// Provides model endpoint related database operations.
/// </summary>
public interface IModelEndpointDataService
{
	/// <summary>
	/// Creates a new model endpoint.
	/// </summary>
	/// <param name="publicId">The public identifier to store.</param>
	/// <param name="providerType">The endpoint type/protocol identifier.</param>
	/// <param name="baseUrl">The base URL of the endpoint.</param>
	/// <param name="name">The human-friendly name.</param>
	/// <param name="description">An optional description.</param>
	/// <param name="credentials">
	/// Optional plaintext credentials to protect and store in <see cref="ModelEndpointEntity.EncryptedCredentials"/>.
	/// </param>
	/// <param name="utcNow">The timestamp to store as creation time.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The created <see cref="ModelEndpointEntity"/>.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="providerType"/>, <paramref name="baseUrl"/>, or <paramref name="name"/> is
	/// empty/whitespace after trimming.
	/// </exception>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<ModelEndpointEntity> CreateModelEndpointAsync(
		Guid              publicId,
		string            providerType,
		string            baseUrl,
		string            name,
		string?           description,
		string?           credentials,
		DateTime          utcNow,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a model endpoint by its public identifier.
	/// </summary>
	/// <param name="publicId">The public identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The matching endpoint, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<ModelEndpointEntity?> GetModelEndpointByPublicIdAsync(
		Guid              publicId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a model endpoint by its internal identifier.
	/// </summary>
	/// <param name="endpointId">The internal endpoint identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The matching endpoint, or <see langword="null"/> if not found.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="endpointId"/> is less than or equal to 0.</exception>
	Task<ModelEndpointEntity?> GetModelEndpointByIdAsync(
		ModelEndpointId   endpointId,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks whether a model endpoint exists.
	/// </summary>
	/// <param name="endpointId">The internal endpoint identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the endpoint exists; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="endpointId"/> is less than or equal to 0.</exception>
	Task<bool> ModelEndpointExistsAsync(ModelEndpointId endpointId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks whether a model endpoint exists.
	/// </summary>
	/// <param name="publicId">The public identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the endpoint exists; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="publicId"/> is <see cref="Guid.Empty"/>.</exception>
	Task<bool> ModelEndpointExistsByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Lists all model endpoints.
	/// </summary>
	/// <param name="includeInactive">
	/// <see langword="true"/> to include inactive endpoints; otherwise only active endpoints are returned.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The endpoints ordered by name.</returns>
	Task<List<ModelEndpointEntity>> ListModelEndpointsAsync(
		bool              includeInactive,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates the human-facing metadata of an endpoint.
	/// </summary>
	/// <param name="endpointId">The internal endpoint identifier.</param>
	/// <param name="name">The new name.</param>
	/// <param name="description">The new description.</param>
	/// <param name="isActive">Whether the endpoint should be active.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the endpoint existed and was updated; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="endpointId"/> is less than or equal to 0.</exception>
	/// <exception cref="ArgumentException"><paramref name="name"/> is empty/whitespace after trimming.</exception>
	Task<bool> UpdateModelEndpointMetadataAsync(
		ModelEndpointId   endpointId,
		string            name,
		string?           description,
		bool              isActive,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates the stored credentials for an endpoint.
	/// </summary>
	/// <param name="endpointId">The internal endpoint identifier.</param>
	/// <param name="credentials">
	/// The new plaintext credentials to protect and persist.
	/// Set to <see langword="null"/> to remove stored credentials.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the endpoint existed and was updated; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="endpointId"/> is less than or equal to 0.</exception>
	Task<bool> UpdateModelEndpointCredentialsAsync(
		ModelEndpointId   endpointId,
		string?           credentials,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates an existing model endpoint.
	/// </summary>
	/// <param name="endpointId">The internal endpoint identifier.</param>
	/// <param name="name">The new name.</param>
	/// <param name="description">The new description.</param>
	/// <param name="isActive">Whether the endpoint should be active.</param>
	/// <param name="credentials">
	/// The new plaintext credentials to protect and persist.
	/// Set to <see langword="null"/> to remove stored credentials.
	/// </param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns><see langword="true"/> if the endpoint existed and was updated; otherwise <see langword="false"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="endpointId"/> is less than or equal to 0.</exception>
	/// <exception cref="ArgumentException"><paramref name="name"/> is empty/whitespace after trimming.</exception>
	/// <remarks>
	/// This update method intentionally does not allow changing <see cref="ModelEndpointEntity.ProviderType"/> or
	/// <see cref="ModelEndpointEntity.BaseUrl"/> to preserve historical reproducibility. To move an endpoint to a new
	/// URL/protocol, create a new endpoint and deactivate the old one.
	/// </remarks>
	Task<bool> UpdateModelEndpointAsync(
		ModelEndpointId   endpointId,
		string            name,
		string?           description,
		bool              isActive,
		string?           credentials,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the decrypted credentials for an endpoint.
	/// </summary>
	/// <param name="endpointId">The internal endpoint identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The plaintext credentials, or <see langword="null"/> if the endpoint does not exist or has no stored credentials.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="endpointId"/> is less than or equal to 0.</exception>
	/// <exception cref="FormatException">Stored credentials are not in a supported format.</exception>
	/// <exception cref="CryptographicException">Authentication or decryption fails.</exception>
	Task<string?> GetModelEndpointCredentialsAsync(
		ModelEndpointId   endpointId,
		CancellationToken cancellationToken = default);
}
