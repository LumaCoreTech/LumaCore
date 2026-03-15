// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Entities;
using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace LumaCore.Data.Tests.Services;

public sealed partial class LumaCoreDataServiceTests
{
	/// <summary>
	/// Tests for <see cref="IModelEndpointDataService"/> methods.
	/// </summary>
	/// <remarks>
	/// These tests cover the full lifecycle of model endpoints: creation, lookup, existence checks, listing,
	/// metadata updates, and credential management (encrypt-at-rest / decrypt-on-read roundtrips).
	/// </remarks>
	[Trait("Category", "Data")]
	public sealed class ModelEndpoints : TestBase
	{
		#region CreateModelEndpointAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> persists a fully populated
		/// endpoint with <see cref="ModelEndpointEntity.IsActive"/> defaulting to <see langword="true"/> and
		/// <see cref="ModelEndpointEntity.EncryptedCredentials"/> set to <see langword="null"/> when no credentials
		/// are provided.
		/// </summary>
		[Fact]
		public async Task
			CreateModelEndpointAsync_WhenValidWithoutCredentials_CreatesActiveEndpointWithNullCredentials()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			var publicId = Guid.NewGuid();
			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: publicId,
				                               providerType: "ollama",
				                               baseUrl: "https://example.test/api",
				                               name: "Test Endpoint",
				                               description: "A test endpoint.",
				                               credentials: null,
				                               utcNow: utcNow);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == endpoint.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal(publicId, reloaded.PublicId);
			Assert.Equal("ollama", reloaded.ProviderType);
			Assert.Equal("https://example.test/api", reloaded.BaseUrl);
			Assert.Equal("Test Endpoint", reloaded.Name);
			Assert.Equal("A test endpoint.", reloaded.Description);
			Assert.True(reloaded.IsActive);
			Assert.Equal(utcNow, reloaded.CreatedAtUtc);
			Assert.Null(reloaded.EncryptedCredentials);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> encrypts credentials before
		/// persisting them, ensuring the stored value differs from the plaintext input.
		/// </summary>
		[Fact]
		public async Task CreateModelEndpointAsync_WhenCredentialsProvided_StoresEncryptedCredentials()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			const string plaintext = "sk-secret-api-key-12345";

			// Act
			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: Guid.NewGuid(),
				                               providerType: "openai-compatible",
				                               baseUrl: "https://example.test/v1",
				                               name: "Endpoint",
				                               description: null,
				                               credentials: plaintext,
				                               utcNow: utcNow);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == endpoint.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.NotNull(reloaded.EncryptedCredentials);
			Assert.NotEqual(plaintext, reloaded.EncryptedCredentials);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> normalizes whitespace-only
		/// description to <see langword="null"/>.
		/// </summary>
		[Fact]
		public async Task CreateModelEndpointAsync_WhenDescriptionWhitespace_BecomesNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: Guid.NewGuid(),
				                               providerType: "ollama",
				                               baseUrl: "https://example.test/api",
				                               name: "Endpoint",
				                               description: "   ",
				                               credentials: null,
				                               utcNow: utcNow);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == endpoint.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Null(reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> trims leading and trailing
		/// whitespace from providerType, baseUrl, name, and description.
		/// </summary>
		[Fact]
		public async Task CreateModelEndpointAsync_WhenValid_TrimsFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Act
			ModelEndpointEntity endpoint = await service.CreateModelEndpointAsync(
				                               publicId: Guid.NewGuid(),
				                               providerType: "  ollama  ",
				                               baseUrl: "  https://example.test/api  ",
				                               name: "  Test Endpoint  ",
				                               description: "  A description  ",
				                               credentials: null,
				                               utcNow: utcNow);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == endpoint.Id);

			// Assert
			Assert.NotNull(reloaded);
			Assert.Equal("ollama", reloaded.ProviderType);
			Assert.Equal("https://example.test/api", reloaded.BaseUrl);
			Assert.Equal("Test Endpoint", reloaded.Name);
			Assert.Equal("A description", reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> throws
		/// <see cref="ArgumentException"/> when <c>publicId</c> is <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task CreateModelEndpointAsync_WhenPublicIdEmpty_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateModelEndpointAsync(
					         publicId: Guid.Empty,
					         providerType: "ollama",
					         baseUrl: "https://example.test/api",
					         name: "Endpoint",
					         description: null,
					         credentials: null,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal("publicId", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="CreateModelEndpointAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid combination of fields that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string, string, string, string?, string>
			CreateModelEndpointAsync_InvalidInput_Data => new()
		{
			// Whitespace-only provider type
			{ "Whitespace providerType", "   ", "https://example.test/api", "Endpoint", null, "providerType" },

			// Provider type exceeds the 50-character maximum
			{
				"Provider type too long", new string('x', 51), "https://example.test/api", "Endpoint", null,
				"providerType"
			},

			// Whitespace-only base URL
			{ "Whitespace baseUrl", "ollama", "   ", "Endpoint", null, "baseUrl" },

			// Base URL exceeds the 500-character maximum
			{ "Base URL too long", "ollama", new string('x', 501), "Endpoint", null, "baseUrl" },

			// Whitespace-only name
			{ "Whitespace name", "ollama", "https://example.test/api", "   ", null, "name" },

			// Name exceeds the 100-character maximum
			{ "Name too long", "ollama", "https://example.test/api", new string('x', 101), null, "name" },

			// Description exceeds the 1000-character maximum
			{
				"Description too long", "ollama", "https://example.test/api", "Endpoint", new string('x', 1001),
				"description"
			}
		};

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> rejects invalid inputs
		/// with an <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="providerType">The provider type to pass to the method.</param>
		/// <param name="baseUrl">The base URL to pass to the method.</param>
		/// <param name="name">The name to pass to the method.</param>
		/// <param name="description">The description to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(CreateModelEndpointAsync_InvalidInput_Data))]
		public async Task CreateModelEndpointAsync_WhenInputInvalid_ThrowsArgumentException(
			string  scenario,
			string  providerType,
			string  baseUrl,
			string  name,
			string? description,
			string  expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.CreateModelEndpointAsync(
					         publicId: Guid.NewGuid(),
					         providerType: providerType,
					         baseUrl: baseUrl,
					         name: name,
					         description: description,
					         credentials: null,
					         utcNow: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region GetModelEndpointByPublicIdAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointByPublicIdAsync"/> returns the endpoint
		/// when it exists.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointByPublicIdAsync_WhenFound_ReturnsEndpoint()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			ModelEndpointEntity? loaded = await service.GetModelEndpointByPublicIdAsync(created.PublicId);

			// Assert
			Assert.NotNull(loaded);
			Assert.Equal(created.Id, loaded.Id);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointByPublicIdAsync"/> returns
		/// <see langword="null"/> when no endpoint with the specified public id exists.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointByPublicIdAsync_WhenNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			ModelEndpointEntity? loaded = await service.GetModelEndpointByPublicIdAsync(Guid.NewGuid());

			// Assert
			Assert.Null(loaded);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointByPublicIdAsync"/> throws
		/// <see cref="ArgumentException"/> when <c>publicId</c> is <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointByPublicIdAsync_WhenPublicIdEmpty_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.GetModelEndpointByPublicIdAsync(Guid.Empty));
			Assert.Equal("publicId", ex.ParamName);
		}

		#endregion

		#region GetModelEndpointByIdAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointByIdAsync"/> returns the endpoint when
		/// it exists.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointByIdAsync_WhenFound_ReturnsEndpoint()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			ModelEndpointEntity? loaded = await service.GetModelEndpointByIdAsync(created.Id);

			// Assert
			Assert.NotNull(loaded);
			Assert.Equal(created.PublicId, loaded.PublicId);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointByIdAsync"/> returns
		/// <see langword="null"/> when no endpoint with the specified id exists.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointByIdAsync_WhenNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			ModelEndpointEntity? loaded = await service.GetModelEndpointByIdAsync(new ModelEndpointId(12345));

			// Assert
			Assert.Null(loaded);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointByIdAsync"/> validates the endpoint id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointByIdAsync_WhenEndpointIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetModelEndpointByIdAsync(new ModelEndpointId(0)));
			Assert.Equal("endpointId.Value", ex.ParamName);
		}

		#endregion

		#region ModelEndpointExistsAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ModelEndpointExistsAsync"/> returns
		/// <see langword="true"/> when the endpoint exists.
		/// </summary>
		[Fact]
		public async Task ModelEndpointExistsAsync_WhenExists_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool exists = await service.ModelEndpointExistsAsync(created.Id);

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ModelEndpointExistsAsync"/> returns
		/// <see langword="false"/> when the endpoint does not exist.
		/// </summary>
		[Fact]
		public async Task ModelEndpointExistsAsync_WhenNotExists_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool exists = await service.ModelEndpointExistsAsync(new ModelEndpointId(12345));

			// Assert
			Assert.False(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ModelEndpointExistsAsync"/> validates the endpoint id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task ModelEndpointExistsAsync_WhenEndpointIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.ModelEndpointExistsAsync(new ModelEndpointId(0)));
			Assert.Equal("endpointId.Value", ex.ParamName);
		}

		#endregion

		#region ModelEndpointExistsByPublicIdAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ModelEndpointExistsByPublicIdAsync"/> returns
		/// <see langword="true"/> when the endpoint exists.
		/// </summary>
		[Fact]
		public async Task ModelEndpointExistsByPublicIdAsync_WhenExists_ReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool exists = await service.ModelEndpointExistsByPublicIdAsync(created.PublicId);

			// Assert
			Assert.True(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ModelEndpointExistsByPublicIdAsync"/> returns
		/// <see langword="false"/> when the endpoint does not exist.
		/// </summary>
		[Fact]
		public async Task ModelEndpointExistsByPublicIdAsync_WhenNotExists_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool exists = await service.ModelEndpointExistsByPublicIdAsync(Guid.NewGuid());

			// Assert
			Assert.False(exists);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ModelEndpointExistsByPublicIdAsync"/> throws
		/// <see cref="ArgumentException"/> when <c>publicId</c> is <see cref="Guid.Empty"/>.
		/// </summary>
		[Fact]
		public async Task ModelEndpointExistsByPublicIdAsync_WhenPublicIdEmpty_ThrowsArgumentException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.ModelEndpointExistsByPublicIdAsync(Guid.Empty));
			Assert.Equal("publicId", ex.ParamName);
		}

		#endregion

		#region ListModelEndpointsAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ListModelEndpointsAsync"/> returns only active
		/// endpoints when <c>includeInactive</c> is <see langword="false"/>, ordered by name.
		/// </summary>
		[Fact]
		public async Task ListModelEndpointsAsync_WhenIncludeInactiveFalse_ReturnsOnlyActiveOrderedByName()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			// Seed two active endpoints (B before A in insertion order, but A before B alphabetically).
			await service.CreateModelEndpointAsync(
				Guid.NewGuid(),
				"ollama",
				"https://example.test/b",
				"Bravo",
				null,
				null,
				utcNow);

			await service.CreateModelEndpointAsync(
				Guid.NewGuid(),
				"ollama",
				"https://example.test/a",
				"Alpha",
				null,
				null,
				utcNow);

			// Seed one inactive endpoint via direct DB manipulation.
			var inactive = new ModelEndpointEntity
			{
				PublicId = Guid.NewGuid(),
				ProviderType = "ollama",
				BaseUrl = "https://example.test/inactive",
				Name = "Inactive",
				IsActive = false,
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.ModelEndpoints.Add(inactive);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			List<ModelEndpointEntity> result = await service.ListModelEndpointsAsync(includeInactive: false);

			// Assert
			Assert.Equal(2, result.Count);
			Assert.Equal("Alpha", result[0].Name);
			Assert.Equal("Bravo", result[1].Name);
			Assert.DoesNotContain(result, e => e.Name == "Inactive");
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.ListModelEndpointsAsync"/> returns all endpoints
		/// including inactive ones when <c>includeInactive</c> is <see langword="true"/>.
		/// </summary>
		[Fact]
		public async Task ListModelEndpointsAsync_WhenIncludeInactiveTrue_ReturnsAllEndpoints()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			await service.CreateModelEndpointAsync(
				Guid.NewGuid(),
				"ollama",
				"https://example.test/a",
				"Active",
				null,
				null,
				utcNow);

			var inactive = new ModelEndpointEntity
			{
				PublicId = Guid.NewGuid(),
				ProviderType = "ollama",
				BaseUrl = "https://example.test/inactive",
				Name = "Inactive",
				IsActive = false,
				CreatedAtUtc = utcNow
			};
			Fixture.DbContext.ModelEndpoints.Add(inactive);
			await Fixture.DbContext.SaveChangesAsync();

			// Act
			List<ModelEndpointEntity> result = await service.ListModelEndpointsAsync(includeInactive: true);

			// Assert
			Assert.Equal(2, result.Count);
			Assert.Contains(result, e => e.Name == "Active");
			Assert.Contains(result, e => e.Name == "Inactive");
		}

		#endregion

		#region UpdateModelEndpointMetadataAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointMetadataAsync"/> updates name,
		/// description, and isActive when the endpoint exists and returns <see langword="true"/>.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointMetadataAsync_WhenEndpointExists_UpdatesAndReturnsTrue()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Original",
				                              description: "Original description",
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointMetadataAsync(
				               endpointId: created.Id,
				               name: "Renamed",
				               description: "New description",
				               isActive: false);

			// Assert
			Assert.True(updated);

			// Special: UpdateModelEndpointMetadataAsync() uses ExecuteUpdateAsync() (set-based update), so we reload.
			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Name);
			Assert.Equal("New description", reloaded.Description);
			Assert.False(reloaded.IsActive);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointMetadataAsync"/> trims leading and
		/// trailing whitespace from name and description before persisting.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointMetadataAsync_WhenValid_TrimsFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Original",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointMetadataAsync(
				               endpointId: created.Id,
				               name: "  Renamed  ",
				               description: "  New description  ",
				               isActive: true);

			// Special: UpdateModelEndpointMetadataAsync() trims name and description via Guard helpers.
			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			// Assert
			Assert.True(updated);
			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Name);
			Assert.Equal("New description", reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointMetadataAsync"/> normalizes
		/// whitespace-only description to <see langword="null"/>.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointMetadataAsync_WhenDescriptionWhitespace_BecomesNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: "Original description",
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointMetadataAsync(
				               endpointId: created.Id,
				               name: "Endpoint",
				               description: "   ",
				               isActive: true);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			// Assert
			Assert.True(updated);
			Assert.NotNull(reloaded);
			Assert.Null(reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointMetadataAsync"/> returns
		/// <see langword="false"/> when the endpoint does not exist.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointMetadataAsync_WhenEndpointDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool updated = await service.UpdateModelEndpointMetadataAsync(
				               endpointId: new ModelEndpointId(12345),
				               name: "Name",
				               description: null,
				               isActive: true);

			// Assert
			Assert.False(updated);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointMetadataAsync"/> validates the
		/// endpoint id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task
			UpdateModelEndpointMetadataAsync_WhenEndpointIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.UpdateModelEndpointMetadataAsync(
					         endpointId: new ModelEndpointId(0),
					         name: "Name",
					         description: null,
					         isActive: true));
			Assert.Equal("endpointId.Value", ex.ParamName);
		}

		/// <summary>
		/// Test data for
		/// <see cref="UpdateModelEndpointMetadataAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid name or description that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string, string?, string> UpdateModelEndpointMetadataAsync_InvalidInput_Data =>
			new()
			{
				// Whitespace-only name
				{ "Whitespace name", "   ", null, "name" },

				// Name exceeds the 100-character maximum
				{ "Name too long", new string('x', 101), null, "name" },

				// Description exceeds the 1000-character maximum
				{ "Description too long", "Valid", new string('x', 1001), "description" }
			};

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointMetadataAsync"/> rejects invalid
		/// inputs with an <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="name">The name to pass to the method.</param>
		/// <param name="description">The description to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(UpdateModelEndpointMetadataAsync_InvalidInput_Data))]
		public async Task UpdateModelEndpointMetadataAsync_WhenInputInvalid_ThrowsArgumentException(
			string  scenario,
			string  name,
			string? description,
			string  expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.UpdateModelEndpointMetadataAsync(
					         endpointId: new ModelEndpointId(1),
					         name: name,
					         description: description,
					         isActive: true));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region UpdateModelEndpointAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> updates all fields
		/// including encrypted credentials when the endpoint exists.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointAsync_WhenEndpointExistsWithCredentials_UpdatesAllFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Original",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointAsync(
				               endpointId: created.Id,
				               name: "Renamed",
				               description: "New description",
				               isActive: false,
				               credentials: "new-secret");

			// Assert
			Assert.True(updated);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Name);
			Assert.Equal("New description", reloaded.Description);
			Assert.False(reloaded.IsActive);
			Assert.NotNull(reloaded.EncryptedCredentials);
			Assert.NotEqual("new-secret", reloaded.EncryptedCredentials);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> clears stored credentials
		/// when <see langword="null"/> is passed.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointAsync_WhenCredentialsNull_ClearsStoredCredentials()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: "initial-secret",
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointAsync(
				               endpointId: created.Id,
				               name: "Endpoint",
				               description: null,
				               isActive: true,
				               credentials: null);

			// Assert
			Assert.True(updated);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			Assert.NotNull(reloaded);
			Assert.Null(reloaded.EncryptedCredentials);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> trims leading and trailing
		/// whitespace from name and description before persisting.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointAsync_WhenValid_TrimsFields()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Original",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointAsync(
				               endpointId: created.Id,
				               name: "  Renamed  ",
				               description: "  New description  ",
				               isActive: true,
				               credentials: null);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			// Assert
			Assert.True(updated);
			Assert.NotNull(reloaded);
			Assert.Equal("Renamed", reloaded.Name);
			Assert.Equal("New description", reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> normalizes whitespace-only
		/// description to <see langword="null"/>.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointAsync_WhenDescriptionWhitespace_BecomesNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: "Original description",
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointAsync(
				               endpointId: created.Id,
				               name: "Endpoint",
				               description: "   ",
				               isActive: true,
				               credentials: null);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			// Assert
			Assert.True(updated);
			Assert.NotNull(reloaded);
			Assert.Null(reloaded.Description);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> returns
		/// <see langword="false"/> when the endpoint does not exist.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointAsync_WhenEndpointDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool updated = await service.UpdateModelEndpointAsync(
				               endpointId: new ModelEndpointId(12345),
				               name: "Name",
				               description: null,
				               isActive: true,
				               credentials: null);

			// Assert
			Assert.False(updated);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> validates the endpoint id
		/// and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointAsync_WhenEndpointIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.UpdateModelEndpointAsync(
					         endpointId: new ModelEndpointId(0),
					         name: "Name",
					         description: null,
					         isActive: true,
					         credentials: null));
			Assert.Equal("endpointId.Value", ex.ParamName);
		}

		/// <summary>
		/// Test data for <see cref="UpdateModelEndpointAsync_WhenInputInvalid_ThrowsArgumentException"/>. Each row
		/// provides an invalid name or description that triggers an <see cref="ArgumentException"/>.
		/// </summary>
		public static TheoryData<string, string, string?, string> UpdateModelEndpointAsync_InvalidInput_Data => new()
		{
			// Whitespace-only name
			{ "Whitespace name", "   ", null, "name" },

			// Name exceeds the 100-character maximum
			{ "Name too long", new string('x', 101), null, "name" },

			// Description exceeds the 1000-character maximum
			{ "Description too long", "Valid", new string('x', 1001), "description" }
		};

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointAsync"/> rejects invalid inputs with
		/// an <see cref="ArgumentException"/>.
		/// </summary>
		/// <param name="scenario">A human-readable description of the test case.</param>
		/// <param name="name">The name to pass to the method.</param>
		/// <param name="description">The description to pass to the method.</param>
		/// <param name="expectedParamName">The expected <see cref="ArgumentException.ParamName"/>.</param>
		[Theory]
		[MemberData(nameof(UpdateModelEndpointAsync_InvalidInput_Data))]
		public async Task UpdateModelEndpointAsync_WhenInputInvalid_ThrowsArgumentException(
			string  scenario,
			string  name,
			string? description,
			string  expectedParamName)
		{
			_ = scenario;

			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
				         service.UpdateModelEndpointAsync(
					         endpointId: new ModelEndpointId(1),
					         name: name,
					         description: description,
					         isActive: true,
					         credentials: null));
			Assert.Equal(expectedParamName, ex.ParamName);
		}

		#endregion

		#region UpdateModelEndpointCredentialsAsync

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointCredentialsAsync"/> encrypts and
		/// persists credentials when a non-null value is provided.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointCredentialsAsync_WhenCredentialsProvided_StoresEncrypted()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointCredentialsAsync(
				               endpointId: created.Id,
				               credentials: "new-api-key");

			// Assert
			Assert.True(updated);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			Assert.NotNull(reloaded);
			Assert.NotNull(reloaded.EncryptedCredentials);
			Assert.NotEqual("new-api-key", reloaded.EncryptedCredentials);
		}

		/// <summary>
		/// Verifies the encrypt-at-rest / decrypt-on-read roundtrip when credentials are set via
		/// <see cref="IModelEndpointDataService.UpdateModelEndpointCredentialsAsync"/> and then read back via
		/// <see cref="IModelEndpointDataService.GetModelEndpointCredentialsAsync"/>.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointCredentialsAsync_WhenRoundtripped_ReturnsOriginalPlaintext()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			const string plaintext = "updated-secret-key-67890";

			// Act
			bool updated = await service.UpdateModelEndpointCredentialsAsync(created.Id, plaintext);
			string? decrypted = await service.GetModelEndpointCredentialsAsync(created.Id);

			// Assert
			Assert.True(updated);
			Assert.Equal(plaintext, decrypted);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointCredentialsAsync"/> clears stored
		/// credentials when <see langword="null"/> is provided.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointCredentialsAsync_WhenCredentialsNull_ClearsStoredCredentials()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: "initial-secret",
				                              utcNow: utcNow);

			// Act
			bool updated = await service.UpdateModelEndpointCredentialsAsync(
				               endpointId: created.Id,
				               credentials: null);

			// Assert
			Assert.True(updated);

			ModelEndpointEntity? reloaded = await Fixture.DbContext.ModelEndpoints
				                                .AsNoTracking()
				                                .FirstOrDefaultAsync(e => e.Id == created.Id);

			Assert.NotNull(reloaded);
			Assert.Null(reloaded.EncryptedCredentials);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointCredentialsAsync"/> returns
		/// <see langword="false"/> when the endpoint does not exist.
		/// </summary>
		[Fact]
		public async Task UpdateModelEndpointCredentialsAsync_WhenEndpointDoesNotExist_ReturnsFalse()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			bool updated = await service.UpdateModelEndpointCredentialsAsync(
				               endpointId: new ModelEndpointId(12345),
				               credentials: "secret");

			// Assert
			Assert.False(updated);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.UpdateModelEndpointCredentialsAsync"/> validates the
		/// endpoint id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task
			UpdateModelEndpointCredentialsAsync_WhenEndpointIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.UpdateModelEndpointCredentialsAsync(
					         endpointId: new ModelEndpointId(0),
					         credentials: "secret"));
			Assert.Equal("endpointId.Value", ex.ParamName);
		}

		#endregion

		#region GetModelEndpointCredentialsAsync

		/// <summary>
		/// Verifies the full encrypt-at-rest / decrypt-on-read roundtrip:
		/// <see cref="IModelEndpointDataService.CreateModelEndpointAsync"/> stores encrypted credentials, and
		/// <see cref="IModelEndpointDataService.GetModelEndpointCredentialsAsync"/> returns the original plaintext.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointCredentialsAsync_WhenCredentialsExist_ReturnsDecryptedPlaintext()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			const string plaintext = "sk-secret-api-key-12345";

			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "openai-compatible",
				                              baseUrl: "https://example.test/v1",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: plaintext,
				                              utcNow: utcNow);

			// Act
			string? decrypted = await service.GetModelEndpointCredentialsAsync(created.Id);

			// Assert
			Assert.Equal(plaintext, decrypted);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointCredentialsAsync"/> returns
		/// <see langword="null"/> when the endpoint has no stored credentials.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointCredentialsAsync_WhenNoCredentials_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			DateTime utcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			ModelEndpointEntity created = await service.CreateModelEndpointAsync(
				                              publicId: Guid.NewGuid(),
				                              providerType: "ollama",
				                              baseUrl: "https://example.test/api",
				                              name: "Endpoint",
				                              description: null,
				                              credentials: null,
				                              utcNow: utcNow);

			// Act
			string? decrypted = await service.GetModelEndpointCredentialsAsync(created.Id);

			// Assert
			Assert.Null(decrypted);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointCredentialsAsync"/> returns
		/// <see langword="null"/> when the endpoint does not exist.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointCredentialsAsync_WhenEndpointNotFound_ReturnsNull()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act
			string? decrypted = await service.GetModelEndpointCredentialsAsync(new ModelEndpointId(12345));

			// Assert
			Assert.Null(decrypted);
		}

		/// <summary>
		/// Verifies that <see cref="IModelEndpointDataService.GetModelEndpointCredentialsAsync"/> validates the
		/// endpoint id and throws <see cref="ArgumentOutOfRangeException"/> for non-positive ids.
		/// </summary>
		[Fact]
		public async Task GetModelEndpointCredentialsAsync_WhenEndpointIdInvalid_ThrowsArgumentOutOfRangeException()
		{
			// Arrange
			LumaCoreDataService service = LumaCoreDataServiceFactory.Create(Fixture.DbContext);

			// Act + Assert
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				         service.GetModelEndpointCredentialsAsync(new ModelEndpointId(0)));
			Assert.Equal("endpointId.Value", ex.ParamName);
		}

		#endregion
	}
}
