// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Initialization;

using Xunit;

namespace LumaCore.Data.Tests.Initialization;

/// <summary>
/// Unit tests for <see cref="DatabaseConnectionInterceptor"/>.
/// </summary>
/// <remarks>
///     <para>
///     Tests exercise the core failure-handling logic via the <c>internal</c> methods
///     <see cref="DatabaseConnectionInterceptor.HandleCommandFailure"/> and
///     <see cref="DatabaseConnectionInterceptor.HandleConnectionFailure"/>, bypassing the
///     EF Core interceptor entry points which require complex event data types.
///     </para>
///     <list type="bullet">
///         <item>
///         <c>DatabaseConnectionInterceptorTests.HandleCommandFailure.cs</c> — Command failure
///         detection with provider-specific error filtering
///         </item>
///         <item>
///         <c>DatabaseConnectionInterceptorTests.HandleConnectionFailure.cs</c> — Connection failure
///         detection with cancellation filtering and sliding window
///         </item>
///     </list>
/// </remarks>
[Trait("Category", "Initialization")]
public sealed partial class DatabaseConnectionInterceptorTests
{
	#region ResetFailureCounter()

	/// <summary>
	/// Verifies that <see cref="DatabaseConnectionInterceptor.ResetFailureCounter"/> clears all recorded
	/// failure timestamps, so previously accumulated failures no longer count toward the threshold.
	/// </summary>
	[Fact]
	public void ResetFailureCounter_AfterFailures_ClearsTimestamps()
	{
		// Arrange — threshold=3, accumulate 2 failures first
		DatabaseInitializationStatus status = CreateCompletedStatus();
		DatabaseConnectionInterceptor sut = CreateInterceptor(status, failureThreshold: 3);
		var exception = new InvalidOperationException("connection refused");

		sut.HandleConnectionFailure(exception);
		sut.HandleConnectionFailure(exception);

		// Act — reset, then add 1 more failure (total would be 3 without reset)
		sut.ResetFailureCounter();
		sut.HandleConnectionFailure(exception);

		// Assert — only 1 failure after reset, threshold (3) not reached
		Assert.Equal(DatabaseInitializationState.Completed, status.State);
		Assert.True(status.IsReady);
	}

	#endregion
}
