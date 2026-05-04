// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Tests.Infrastructure;

using Xunit;

namespace LumaCore.Data.Tests;

/// <summary>
/// Tests for <see cref="LumaCoreDbContext"/>. The class is split across multiple partial files,
/// each covering one chapter of the context's behaviour:
/// <list type="number">
///     <item>
///     <description>
///     <b>ModelMetadata</b> (<c>LumaCoreDbContextTests.ModelMetadata.cs</c>) — provider-specific
///     model configuration that survives the round-trip into the runtime <c>IModel</c>: the
///     <c>Users.Email</c> unique index filter syntax (SQLite vs. SQL Server) and the
///     <c>MessageEntity.Type</c> default value.
///     </description>
///     </item>
///     <item>
///     <description>
///     <b>Compensations</b> (<c>LumaCoreDbContextTests.Compensations.cs</c>) — the cross-resource
///     atomicity API (<see cref="LumaCoreDbContext.RegisterRollbackCompensation"/>,
///     <see cref="LumaCoreDbContext.UnregisterRollbackCompensation"/>,
///     <see cref="LumaCoreDbContext.BeginCompensatingTransactionAsync"/>, and the
///     <see cref="ICompensatingTransaction"/> wrapper's commit/rollback/dispose semantics).
///     </description>
///     </item>
///     <item>
///     <description>
///     <b>TestModels</b> (<c>LumaCoreDbContextTests.TestModels.cs</c>) — shared test fixtures
///     such as the <c>CompensationRecorder</c> helper used by the compensation suite.
///     </description>
///     </item>
/// </list>
/// Reading order: start with ModelMetadata for the static schema-shape invariants, then move on
/// to Compensations for the runtime behaviour that depends on those invariants holding.
/// </summary>
[Trait("Category", "DbContext")]
public sealed partial class LumaCoreDbContextTests : IAsyncLifetime
{
	private readonly DbFixture mFixture = DbFixture.CreateSqliteInMemory();

	/// <summary>
	/// Initializes the database schema for the test instance.
	/// </summary>
	/// <returns>A task that represents the asynchronous initialization operation.</returns>
	public ValueTask InitializeAsync() => mFixture.InitializeAsync();

	/// <summary>
	/// Disposes the underlying database resources.
	/// </summary>
	/// <returns>A task that represents the asynchronous dispose operation.</returns>
	public ValueTask DisposeAsync() => mFixture.DisposeAsync();
}
