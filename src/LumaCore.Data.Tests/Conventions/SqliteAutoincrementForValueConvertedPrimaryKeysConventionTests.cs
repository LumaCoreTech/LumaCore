// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Conventions;
using LumaCore.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Xunit;

namespace LumaCore.Data.Tests.Conventions;

/// <summary>
/// Backstop tests for <see cref="SqliteAutoincrementForValueConvertedPrimaryKeysConvention"/>.
/// </summary>
/// <remarks>
///     <para>
///     The convention compensates for EF Core's SQLite default-strategy resolver, which fails to recognize
///     value-converted strongly-typed identifier primary keys (e.g. <c>UserId</c>) as autoincrement candidates.
///     If a future EF Core release changes the default-strategy resolver, the public
///     <c>SqlitePropertyExtensions.SetValueGenerationStrategy</c> API, or the way
///     <c>SqliteAnnotationProvider</c> reads the strategy, this test surfaces the regression at build/test
///     time instead of silently re-introducing migration drift.
///     </para>
///     <para>
///     The test class is organized as a single narrative: build the real <see cref="LumaCoreDbContext"/>
///     model on SQLite, then verify the <see cref="SqliteValueGenerationStrategy"/> on every primary-key column.
///     Cases:
///     </para>
///     <list type="number">
///         <item>
///         <b>Value-converted single-column PKs (10 entities)</b> — convention path: must be
///         <see cref="SqliteValueGenerationStrategy.Autoincrement"/>.
///         </item>
///         <item>
///         <b>Plain <see cref="long"/> single-column PK</b> (<see cref="SeedHistoryEntity"/>) — base resolver
///         path: must be <see cref="SqliteValueGenerationStrategy.Autoincrement"/> (no converter, the
///         convention skips and the base resolver handles it).
///         </item>
///         <item>
///         <b>Skip — <see cref="string"/> PK</b> (<see cref="RevokedJwtEntity.Jti"/>) — must be
///         <see cref="SqliteValueGenerationStrategy.None"/>.
///         </item>
///         <item>
///         <b>Skip — singleton with <c>ValueGeneratedNever()</c></b> (<see cref="ResourceGcStateEntity"/>) —
///         must be <see cref="SqliteValueGenerationStrategy.None"/>.
///         </item>
///         <item>
///         <b>Skip — FK-PK on owned-side 1:0..1</b>
///         (<see cref="MessageGenerationMetadataEntity.MessageId"/>, <see cref="UserPreferencesEntity.UserId"/>)
///         — must be <see cref="SqliteValueGenerationStrategy.None"/>.
///         </item>
///         <item>
///         <b>Skip — composite PKs</b> (<see cref="ConversationParticipantEntity"/>,
///         <see cref="UserRoleEntity"/>) — every column must be
///         <see cref="SqliteValueGenerationStrategy.None"/>.
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Conventions")]
public sealed class SqliteAutoincrementForValueConvertedPrimaryKeysConventionTests
{
	/// <summary>
	/// Single-column primary keys whose CLR type is a strongly-typed identifier wrapper backed by a
	/// <see cref="long"/> value converter. The convention must promote each of these to
	/// <see cref="SqliteValueGenerationStrategy.Autoincrement"/>.
	/// </summary>
	public static TheoryData<Type, string> ValueConvertedAutoincrementPks => new()
	{
		{ typeof(ConversationEntity), nameof(ConversationEntity.Id) },
		{ typeof(MessageEntity), nameof(MessageEntity.Id) },
		{ typeof(ModelEndpointEntity), nameof(ModelEndpointEntity.Id) },
		{ typeof(ParticipantEntity), nameof(ParticipantEntity.Id) },
		{ typeof(PersonaEntity), nameof(PersonaEntity.Id) },
		{ typeof(ResourceEntity), nameof(ResourceEntity.Id) },
		{ typeof(ResourceReferenceEntity), nameof(ResourceReferenceEntity.Id) },
		{ typeof(RoleEntity), nameof(RoleEntity.Id) },
		{ typeof(SystemPromptEntity), nameof(SystemPromptEntity.Id) },
		{ typeof(UserEntity), nameof(UserEntity.Id) }
	};

	/// <summary>
	/// Single-column primary keys that the convention deliberately skips and that must remain at
	/// <see cref="SqliteValueGenerationStrategy.None"/>:
	/// FK-PK columns on the owned side of a 1:0..1 relationship, the singleton row with
	/// <c>ValueGeneratedNever()</c>, and the <see cref="string"/>-typed PK.
	/// </summary>
	public static TheoryData<Type, string> SkippedSingleColumnPks => new()
	{
		// FK-PK 1:0..1 owned-side: value is borrowed from the parent row, not generated.
		{ typeof(MessageGenerationMetadataEntity), nameof(MessageGenerationMetadataEntity.MessageId) },
		{ typeof(UserPreferencesEntity), nameof(UserPreferencesEntity.UserId) },

		// Singleton with ValueGeneratedNever() — must not be re-promoted to autoincrement.
		{ typeof(ResourceGcStateEntity), nameof(ResourceGcStateEntity.Id) },

		// String PK — provider type is not long/int, the convention skips it (and base agrees).
		{ typeof(RevokedJwtEntity), nameof(RevokedJwtEntity.Jti) }
	};

	/// <summary>
	/// Composite primary keys: every column must remain at
	/// <see cref="SqliteValueGenerationStrategy.None"/> (autoincrement is meaningless on composite PKs).
	/// </summary>
	public static TheoryData<Type> CompositeKeyEntities => new()
	{
		typeof(ConversationParticipantEntity),
		typeof(UserRoleEntity)
	};

	// --- 1. Value-converted strongly-typed-ID PKs → Autoincrement ---

	/// <summary>
	/// Verifies that every value-converted strongly-typed identifier primary key is promoted to
	/// <see cref="SqliteValueGenerationStrategy.Autoincrement"/> by the convention.
	/// </summary>
	/// <param name="entityClrType">The CLR entity type whose PK is inspected.</param>
	/// <param name="propertyName">The PK property name on the entity.</param>
	[Theory]
	[MemberData(nameof(ValueConvertedAutoincrementPks))]
	public void ProcessModelFinalizing_ValueConvertedSingleColumnPk_SetsAutoincrement(
		Type   entityClrType,
		string propertyName)
	{
		// Arrange
		using LumaCoreDbContext context = CreateSqliteContext();

		// Act — call statically to disambiguate from SqlServerPropertyExtensions.GetValueGenerationStrategy.
		IProperty property = GetPrimaryKeyProperty(context, entityClrType, propertyName);
		SqliteValueGenerationStrategy strategy = SqlitePropertyExtensions.GetValueGenerationStrategy(property);

		// Assert
		Assert.Equal(SqliteValueGenerationStrategy.Autoincrement, strategy);
	}

	// --- 2. Plain long PK

	/// <summary>
	/// Verifies that <see cref="SeedHistoryEntity.Id"/> (plain <see cref="long"/>, no value converter)
	/// is still promoted to <see cref="SqliteValueGenerationStrategy.Autoincrement"/> — the convention
	/// skips it (no converter), but the SQLite default-strategy resolver handles it natively because the
	/// CLR type passes <c>IsInteger()</c>.
	/// </summary>
	[Fact]
	public void ProcessModelFinalizing_PlainLongPk_LeavesBaseResolverAutoincrement()
	{
		// Arrange
		using LumaCoreDbContext context = CreateSqliteContext();

		// Act — call statically to disambiguate from SqlServerPropertyExtensions.GetValueGenerationStrategy.
		IProperty property = GetPrimaryKeyProperty(context, typeof(SeedHistoryEntity), nameof(SeedHistoryEntity.Id));
		SqliteValueGenerationStrategy strategy = SqlitePropertyExtensions.GetValueGenerationStrategy(property);

		// Assert
		Assert.Equal(SqliteValueGenerationStrategy.Autoincrement, strategy);
	}

	// --- 3. Single-column PKs the convention deliberately skips → None ---

	/// <summary>
	/// Verifies that PKs the convention deliberately skips remain at
	/// <see cref="SqliteValueGenerationStrategy.None"/>: FK-PK columns on the owned side of a 1:0..1
	/// relationship, the singleton row with <c>ValueGeneratedNever()</c>, and the
	/// <see cref="string"/>-typed PK.
	/// </summary>
	/// <param name="entityClrType">The CLR entity type whose PK is inspected.</param>
	/// <param name="propertyName">The PK property name on the entity.</param>
	[Theory]
	[MemberData(nameof(SkippedSingleColumnPks))]
	public void ProcessModelFinalizing_SkippedSingleColumnPk_LeavesNone(
		Type   entityClrType,
		string propertyName)
	{
		// Arrange
		using LumaCoreDbContext context = CreateSqliteContext();

		// Act — call statically to disambiguate from SqlServerPropertyExtensions.GetValueGenerationStrategy.
		IProperty property = GetPrimaryKeyProperty(context, entityClrType, propertyName);
		SqliteValueGenerationStrategy strategy = SqlitePropertyExtensions.GetValueGenerationStrategy(property);

		// Assert
		Assert.Equal(SqliteValueGenerationStrategy.None, strategy);
	}

	// --- 4. Composite PKs → every column None ---

	/// <summary>
	/// Verifies that composite primary keys leave every column at
	/// <see cref="SqliteValueGenerationStrategy.None"/> — autoincrement is meaningless on composite PKs,
	/// and the convention's <c>Properties.Count != 1</c> guard must take effect.
	/// </summary>
	/// <param name="entityClrType">The CLR entity type whose composite PK columns are inspected.</param>
	[Theory]
	[MemberData(nameof(CompositeKeyEntities))]
	public void ProcessModelFinalizing_CompositePk_LeavesEveryColumnNone(Type entityClrType)
	{
		// Arrange
		using LumaCoreDbContext context = CreateSqliteContext();
		IEntityType entityType = context.Model.FindEntityType(entityClrType) ??
		                         throw new InvalidOperationException(
			                         $"Entity type '{entityClrType}' not found in the model.");
		IKey primaryKey = entityType.FindPrimaryKey()
		                  ?? throw new InvalidOperationException(
			                  $"Entity '{entityClrType}' has no primary key — composite-key test invariant violated.");

		// Act + Assert — guard the test's own assumption that this entity is composite,
		// then verify every column independently.
		Assert.True(
			primaryKey.Properties.Count > 1,
			$"Expected '{entityClrType.Name}' to have a composite PK, but found {primaryKey.Properties.Count} column(s).");

		foreach (IProperty property in primaryKey.Properties)
		{
			// Static call to disambiguate from SqlServerPropertyExtensions.GetValueGenerationStrategy.
			SqliteValueGenerationStrategy strategy = SqlitePropertyExtensions.GetValueGenerationStrategy(property);
			Assert.Equal(SqliteValueGenerationStrategy.None, strategy);
		}
	}

	// --- Helpers ---

	/// <summary>
	/// Builds a <see cref="LumaCoreDbContext"/> configured for SQLite. The model is built lazily on first
	/// access — no real database connection is required for these tests.
	/// </summary>
	/// <returns>A new context instance; the caller owns disposal.</returns>
	private static LumaCoreDbContext CreateSqliteContext()
	{
		// Connection string points at an in-memory database, but no connection is ever opened — only
		// the model graph is inspected. UseSqlite is required so that the SQLite-specific conventions
		// (including the one under test) are wired in.
		DbContextOptions<LumaCoreDbContext> options = new DbContextOptionsBuilder<LumaCoreDbContext>()
			.UseSqlite("Data Source=:memory:")
			.Options;

		return new LumaCoreDbContext(options);
	}

	/// <summary>
	/// Resolves the primary-key property metadata for a given entity/property pair.
	/// </summary>
	/// <param name="context">The context whose model is inspected.</param>
	/// <param name="entityClrType">The CLR entity type.</param>
	/// <param name="propertyName">The property name on the entity.</param>
	/// <returns>The matching <see cref="IProperty"/> metadata.</returns>
	/// <exception cref="InvalidOperationException">
	/// The entity or property is not present in the model (test invariant violation).
	/// </exception>
	private static IProperty GetPrimaryKeyProperty(
		LumaCoreDbContext context,
		Type              entityClrType,
		string            propertyName)
	{
		IEntityType entityType = context.Model.FindEntityType(entityClrType) ??
		                         throw new InvalidOperationException(
			                         $"Entity type '{entityClrType}' not found in the model.");

		return entityType.FindProperty(propertyName)
		       ?? throw new InvalidOperationException(
			       $"Property '{propertyName}' not found on entity '{entityClrType.Name}'.");
	}
}
