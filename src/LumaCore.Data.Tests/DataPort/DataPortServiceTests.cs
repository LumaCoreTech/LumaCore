// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.Data.Tests.DataPort;

/// <summary>
/// Unit tests for <see cref="DataPortService"/>.
/// </summary>
/// <remarks>
///     <para>The test class is split across partial files:</para>
///     <list type="number">
///         <item>
///         <b>Anchor (this file)</b> — constructor and <see cref="DataPortService.SourceProviderKey"/>.
///         </item>
///         <item>
///         <b>RunExportAsync</b> — <see cref="DataPortService.RunExportAsync"/> progress reporting,
///         parameter validation, cancellation, and general failure.
///         </item>
///         <item>
///         <b>RunImportAsync</b> — <see cref="DataPortService.RunImportAsync"/> progress reporting,
///         parameter validation, empty migration history, schema mismatch, missing shuttle ID,
///         cancellation, and general failure.
///         </item>
///         <item>
///         <b>Helpers</b> — stubs (<c>StubExportReader</c>, <c>StubShuttleWriter</c>,
///         <c>StubShuttleReader</c>, <c>StubImportWriter</c>), <c>CapturingProgress</c>, and
///         <c>EmptyRowStream()</c>.
///         </item>
///     </list>
///     <para>
///     <b>Reading order:</b> Start with the anchor for construction, then RunExportAsync and RunImportAsync
///     for the two pipeline methods, and Helpers for shared test infrastructure.
///     </para>
///     <para>
///     The happy-path orchestration logic (streaming tables, multi-table progress) is covered by integration
///     tests that run against a real SQLite database (see <c>DataPortRoundtripTests</c> and
///     <c>SqliteShuttleRoundtripTests</c>).
///     </para>
/// </remarks>
[Trait("Category", "DataPort")]
public sealed partial class DataPortServiceTests
{
	#region Constructor

	/// <summary>
	/// Verifies that the constructor succeeds when a valid logger is provided.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsValid_CreatesInstance()
	{
		// Arrange + Act
		var sut = new DataPortService(NullLogger<DataPortService>.Instance);

		// Assert
		Assert.NotNull(sut);
	}

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when the logger is
	/// <see langword="null"/>.
	/// </summary>
	[Fact]
	public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DataPortService(null!));
		Assert.Equal("logger", ex.ParamName);
	}

	#endregion

	#region SourceProviderKey

	/// <summary>
	/// Verifies that <see cref="DataPortService.SourceProviderKey"/> has the expected value.
	/// This key is persisted in shuttle metadata and must remain stable.
	/// </summary>
	[Fact]
	public void SourceProviderKey_Always_HasExpectedValue()
	{
		// Act + Assert
		Assert.Equal("SourceProvider", DataPortService.SourceProviderKey);
	}

	#endregion
}
