// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LumaCore.Data.Conventions;

/// <summary>
/// Replaces EF Core's built-in <see cref="SqliteValueGenerationConvention"/> so it recognizes value-converted
/// strongly-typed identifier primary keys (e.g. <c>UserId</c>, <c>MessageId</c>) as integer auto-increment columns.
/// </summary>
/// <remarks>
///     <para>
///     <b>Problem.</b> EF Core's <c>SqlitePropertyExtensions.GetDefaultValueGenerationStrategy</c> only returns
///     <see cref="SqliteValueGenerationStrategy.Autoincrement"/> when the <i>CLR</i> property type passes
///     <c>IsInteger()</c>. With strongly-typed identifiers the CLR type is the wrapper struct (e.g.
///     <c>UserId</c>) — even though the <i>provider</i> type is <see cref="long"/> via a registered
///     <see cref="ValueConverter"/>. The default strategy resolves to <see cref="SqliteValueGenerationStrategy.None"/>,
///     <c>SqliteAnnotationProvider.For(IColumn)</c> consequently emits no <c>Sqlite:Autoincrement</c>, the
///     live model lacks the annotation, and every <c>dotnet ef migrations add</c> reports drift on every PK
///     column (proposing destructive <c>AlterColumn</c> + <c>OldAnnotation</c> calls that would
///     <i>remove</i> <c>AUTOINCREMENT</c> from existing tables).
///     </para>
///     <para>
///     <b>Fix.</b> This subclass is registered via
///     <c>Conventions.Replace&lt;SqliteValueGenerationConvention&gt;(...)</c> in
///     <see cref="DbContext.ConfigureConventions"/>. It runs in the same convention slot as the built-in
///     SQLite convention <i>and</i> additionally implements <see cref="IModelFinalizingConvention"/>. After
///     all conversions are wired up, it walks every PK and explicitly sets the
///     <see cref="SqliteValueGenerationStrategy.Autoincrement"/> strategy on integer-backed PKs whose value
///     converter targets <see cref="long"/> or <see cref="int"/> — the cases the SQLite default-strategy
///     resolver misses because its CLR-type check fails on strongly-typed identifier wrappers. EF's own
///     <c>SqliteAnnotationProvider.For(IColumn)</c> then emits the <c>Sqlite:Autoincrement</c> annotation
///     through its regular code path.
///     </para>
///     <para>
///     <b>Snapshot symmetry.</b> The migration snapshot serializes properties using the <i>provider</i> type
///     (<c>Property&lt;long&gt;("Id")</c>), so when the differ rehydrates the previous model the default-
///     strategy resolver passes its <c>IsInteger()</c> check and independently lands on
///     <see cref="SqliteValueGenerationStrategy.Autoincrement"/>. Both sides converge on the same strategy →
///     no spurious <c>AlterColumn</c> diff.
///     </para>
///     <para>
///     <b>Detection signal.</b>
///     <c>ConventionPropertyExtensions.GetValueConverter(IConventionProperty)</c> is the API that
///     reliably exposes the provider type at finalizing time —
///     <see cref="IReadOnlyProperty.GetProviderClrType"/> is still <see langword="null"/> here because the
///     conversions are registered model-wide via <c>HaveConversion&lt;T&gt;()</c> in
///     <see cref="DbContext.ConfigureConventions"/>, not per-property via <c>HasConversion(...)</c>.
///     </para>
///     <para>
///     Other providers (Npgsql, SqlServer) ignore the SQLite-prefixed annotation, so the model stays portable.
///     </para>
/// </remarks>
sealed class SqliteAutoincrementForValueConvertedPrimaryKeysConvention
	: SqliteValueGenerationConvention, IModelFinalizingConvention
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SqliteAutoincrementForValueConvertedPrimaryKeysConvention"/>
	/// class with the provider and relational dependencies required by the base
	/// <see cref="SqliteValueGenerationConvention"/>.
	/// </summary>
	/// <param name="dependencies">Provider convention-set builder dependencies, injected by EF Core.</param>
	/// <param name="relationalDependencies">Relational convention-set builder dependencies, injected by EF Core.</param>
	public SqliteAutoincrementForValueConvertedPrimaryKeysConvention(
		ProviderConventionSetBuilderDependencies   dependencies,
		RelationalConventionSetBuilderDependencies relationalDependencies)
		: base(dependencies, relationalDependencies) { }

	/// <summary>
	/// Sets <see cref="SqliteValueGenerationStrategy.Autoincrement"/> on integer-backed PK properties whose
	/// CLR type is a strongly-typed identifier wrapper (and therefore missed by the base SQLite default-
	/// strategy resolver).
	/// </summary>
	/// <param name="modelBuilder">The model builder invoking the convention.</param>
	/// <param name="context">The context for the finalization phase.</param>
	public void ProcessModelFinalizing(
		IConventionModelBuilder                     modelBuilder,
		IConventionContext<IConventionModelBuilder> context)
	{
		foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
		{
			IConventionKey? primaryKey = entityType.FindPrimaryKey();
			if (primaryKey is null)
				continue;

			// Single-column integer PKs only — composite keys are not autoincrement candidates.
			if (primaryKey.Properties.Count != 1)
				continue;

			IConventionProperty property = primaryKey.Properties[0];

			// Skip if a value-generation strategy is already configured (e.g. by user code via
			// HasAnnotation, or by a future EF Core change to the default-strategy resolver).
			// Calling the extension method statically: both Sqlite and SqlServer ship a
			// GetValueGenerationStrategyConfigurationSource(IConventionProperty) overload, so the
			// instance-style call is ambiguous.
			if (SqlitePropertyExtensions.GetValueGenerationStrategyConfigurationSource(property) is not null)
				continue;

			// Respect explicit opt-outs: ValueGeneratedNever() (e.g. singleton rows like ResourceGcState)
			// must not be re-promoted to auto-increment.
			if (property.ValueGenerated == ValueGenerated.Never
			    && property.GetValueGeneratedConfigurationSource() is not null)
				continue;

			// Skip FK-PKs (1:0..1 owned-side pattern such as MessageGenerationMetadata.MessageId or
			// UserPreferences.UserId): the value is borrowed from the parent row, not generated by the
			// database. EF's own GetDefaultValueGenerationStrategy skips these for the same reason —
			// matching its behavior keeps the migration differ symmetric.
			if (property.IsForeignKey())
				continue;

			// Detect integer-backed strongly-typed identifiers via the registered value converter.
			// GetProviderClrType() returns null at finalizing time because the conversion is registered
			// model-wide via HaveConversion<T>() in ConfigureConventions, not per-property.
			ValueConverter? converter = property.GetValueConverter();
			if (converter is null)
				continue;

			Type providerType = Nullable.GetUnderlyingType(converter.ProviderClrType) ?? converter.ProviderClrType;
			if (providerType != typeof(long) && providerType != typeof(int))
				continue;

			// Anchor the strategy. The base SqliteValueGenerationConvention's
			// ProcessPropertyAnnotationChanged handler picks this up and propagates ValueGenerated.OnAdd;
			// SqliteAnnotationProvider.For(IColumn) then emits Sqlite:Autoincrement through its regular
			// path because property.GetValueGenerationStrategy() now returns Autoincrement.
			property.SetValueGenerationStrategy(SqliteValueGenerationStrategy.Autoincrement);
		}
	}
}
