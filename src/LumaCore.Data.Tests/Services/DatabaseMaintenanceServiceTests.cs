// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.DataPort;
using LumaCore.Data.Providers;
using LumaCore.Data.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace LumaCore.Data.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DatabaseMaintenanceService"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Partial files (reading order):</b>
///     </para>
///     <list type="number">
///         <item>
///             <term>Anchor (this file)</term>
///             <description>Constructor null-guard tests.</description>
///         </item>
///         <item>
///             <term>GetBackupDirectory</term>
///             <description>
///             Tests for <see cref="DatabaseMaintenanceService.GetBackupDirectory"/> covering absolute paths,
///             relative paths, and the default fallback.
///             </description>
///         </item>
///         <item>
///             <term>CreateShuttleBackupAsync</term>
///             <description>
///             Integration test for <see cref="DatabaseMaintenanceService.CreateShuttleBackupAsync"/> verifying
///             the full export pipeline from source database to shuttle file.
///             </description>
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Services")]
public sealed partial class DatabaseMaintenanceServiceTests
{
	#region Constructor

	/// <summary>
	/// Verifies that the constructor succeeds and stores all dependencies when all parameters are valid.
	/// </summary>
	[Fact]
	public void Constructor_WhenAllParametersValid_CreatesInstance()
	{
		// Arrange
		var logger = NullLogger<DatabaseMaintenanceService>.Instance;
		IOptions<DatabaseOptions> options = Options.Create(new DatabaseOptions());
		var dataPortService = new DataPortService(NullLogger<DataPortService>.Instance);
		IDatabaseProviderOperations providerOperations = new SqliteProviderOperations();
		TimeProvider timeProvider = TimeProvider.System;

		// Act
		var sut = new DatabaseMaintenanceService(logger, options, dataPortService, providerOperations, timeProvider);

		// Assert
		Assert.NotNull(sut);
	}

	/// <summary>
	/// Test data for <see cref="Constructor_WhenRequiredParameterIsNull_ThrowsArgumentNullException"/>:
	/// one row per constructor parameter that has a <see langword="null"/> guard.
	/// </summary>
	public static TheoryData<string> Constructor_NullArguments_Data =>
	[
		"logger",

		// IOptions<DatabaseOptions>
		"options",

		// DataPortService
		"dataPortService",

		// IDatabaseProviderOperations
		"providerOperations",

		// TimeProvider
		"timeProvider"
	];

	/// <summary>
	/// Verifies that the constructor throws <see cref="ArgumentNullException"/> when
	/// <paramref name="paramName"/> identifies a <see langword="null"/> argument.
	/// </summary>
	/// <param name="paramName">The name of the parameter that is <see langword="null"/>.</param>
	[Theory]
	[MemberData(nameof(Constructor_NullArguments_Data))]
	public void Constructor_WhenRequiredParameterIsNull_ThrowsArgumentNullException(string paramName)
	{
		// Arrange — create all valid dependencies, then selectively null out the one identified by paramName.
		var logger = NullLogger<DatabaseMaintenanceService>.Instance;
		IOptions<DatabaseOptions> options = Options.Create(new DatabaseOptions());
		var dataPortService = new DataPortService(NullLogger<DataPortService>.Instance);
		IDatabaseProviderOperations providerOperations = new SqliteProviderOperations();
		TimeProvider timeProvider = TimeProvider.System;

		// Replace exactly one argument with null.
		NullLogger<DatabaseMaintenanceService>? argLogger = paramName == "logger" ? null : logger;
		IOptions<DatabaseOptions>? argOptions = paramName == "options" ? null : options;
		DataPortService? argDataPortService = paramName == "dataPortService" ? null : dataPortService;
		IDatabaseProviderOperations? argProviderOperations =
			paramName == "providerOperations" ? null : providerOperations;
		TimeProvider? argTimeProvider = paramName == "timeProvider" ? null : timeProvider;

		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => new DatabaseMaintenanceService(
			argLogger!,
			argOptions!,
			argDataPortService!,
			argProviderOperations!,
			argTimeProvider!));
		Assert.Equal(paramName, ex.ParamName);
	}

	#endregion
}
