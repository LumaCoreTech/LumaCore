// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Data.Common;
using System.Net.Sockets;

using LumaCore.Data.Providers;

using Npgsql;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class PostgreSqlProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenServiceUnavailable_ReturnsTrue"/>.
	/// Covers PostgreSQL SQLSTATE classes, generic exception types, and nested exception trees.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsTrue_TestData() => new()
	{
		// PostgreSQL: Class 08 — connection exception
		{
			"Class 08: connection_failure (08006)",
			CreateNpgsqlException("08006")
		},

		// PostgreSQL: Class 53 — insufficient resources (too many connections)
		{
			"Class 53: too_many_connections (53300)",
			CreateNpgsqlException("53300")
		},

		// PostgreSQL: Class 57 — operator intervention (admin shutdown)
		{
			"Class 57: admin_shutdown (57P01)",
			CreateNpgsqlException("57P01")
		},

		// PostgreSQL: Class 58 — system error (I/O error)
		{
			"Class 58: io_error (58030)",
			CreateNpgsqlException("58030")
		},

		// Generic: TimeoutException
		{
			"TimeoutException",
			new TimeoutException("Connection timed out")
		},

		// Generic: SocketException
		{
			"SocketException",
			new SocketException((int)SocketError.ConnectionRefused)
		},

		// Generic: EndOfStreamException
		{
			"EndOfStreamException",
			new EndOfStreamException("Unexpected end of stream")
		},

		// Nested: NpgsqlException wrapped in another exception
		{
			"Nested NpgsqlException",
			new InvalidOperationException("Wrapper", CreateNpgsqlException("08006"))
		},

		// Nested: SocketException inside AggregateException
		{
			"SocketException in AggregateException",
			new AggregateException(new SocketException((int)SocketError.HostUnreachable))
		},

		// Deeply nested: AggregateException → InvalidOperationException → NpgsqlException (3 levels).
		// Verifies that depth-first traversal follows InnerException chains inside AggregateException children.
		{
			"Deeply nested NpgsqlException (3 levels)",
			new AggregateException(new InvalidOperationException("Middle", CreateNpgsqlException("08006")))
		},

		// Mixed AggregateException: infrastructure error alongside cancellation.
		// The infrastructure error must not be masked by the OperationCanceledException.
		{
			"AggregateException with NpgsqlException + OperationCanceledException",
			new AggregateException(
				CreateNpgsqlException("08006"),
				new OperationCanceledException("User cancelled"))
		},

		// Mixed AggregateException: cancellation first, infrastructure error second.
		// Verifies traversal order does not affect the result.
		{
			"AggregateException with OperationCanceledException + SocketException",
			new AggregateException(
				new OperationCanceledException("User cancelled"),
				new SocketException((int)SocketError.ConnectionRefused))
		}
	};

	/// <summary>
	/// Verifies that <see cref="PostgreSqlProviderOperations.IsServiceUnavailable"/> returns
	/// <see langword="true"/> for exceptions that indicate the database is unreachable.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="exception">The exception to check.</param>
	[Theory]
	[MemberData(nameof(IsServiceUnavailable_ReturnsTrue_TestData))]
	public void IsServiceUnavailable_WhenServiceUnavailable_ReturnsTrue(string scenario, Exception exception)
	{
		_ = scenario;

		// Arrange
		var sut = new PostgreSqlProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenNotServiceUnavailable_ReturnsFalse"/>.
	/// Covers user cancellations, excluded SQLSTATE codes, and generic exceptions.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsFalse_TestData() => new()
	{
		// OperationCanceledException — user-initiated, not infrastructure failure
		{
			"OperationCanceledException",
			new OperationCanceledException("User cancelled")
		},

		// Nested OperationCanceledException — wrapped by provider
		{
			"Nested OperationCanceledException",
			new InvalidOperationException("Wrapper", new OperationCanceledException())
		},

		// PostgreSQL: 57014 (query_canceled) — explicitly excluded
		{
			"query_canceled (57014)",
			CreateNpgsqlException("57014")
		},

		// PostgreSQL: 57P05 (idle_session_timeout) — explicitly excluded
		{
			"idle_session_timeout (57P05)",
			CreateNpgsqlException("57P05")
		},

		// PostgreSQL: non-connection error (23505 = unique_violation)
		{
			"unique_violation (23505)",
			CreateNpgsqlException("23505")
		},

		// PostgreSQL: null SqlState
		{
			"NpgsqlException with null SqlState",
			CreateNpgsqlException(null)
		},

		// PostgreSQL: empty SqlState
		{
			"NpgsqlException with empty SqlState",
			CreateNpgsqlException("")
		},

		// Generic exception — not connection-related
		{
			"ArgumentException",
			new ArgumentException("Bad argument")
		}
	};

	/// <summary>
	/// Verifies that <see cref="PostgreSqlProviderOperations.IsServiceUnavailable"/> returns
	/// <see langword="false"/> for exceptions that do not indicate service unavailability.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="exception">The exception to check.</param>
	[Theory]
	[MemberData(nameof(IsServiceUnavailable_ReturnsFalse_TestData))]
	public void IsServiceUnavailable_WhenNotServiceUnavailable_ReturnsFalse(string scenario, Exception exception)
	{
		_ = scenario;

		// Arrange
		var sut = new PostgreSqlProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Creates an <see cref="NpgsqlException"/> with the specified SQLSTATE code.
	/// Since <see cref="DbException.SqlState"/> is read-only with no backing field in .NET 10,
	/// we use a derived class that overrides the property.
	/// </summary>
	/// <param name="sqlState">The PostgreSQL SQLSTATE code, or <see langword="null"/>.</param>
	/// <returns>An <see cref="NpgsqlException"/> with the specified SQLSTATE code.</returns>
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
	private static NpgsqlException CreateNpgsqlException(string? sqlState) => new TestNpgsqlException(sqlState);
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

	/// <summary>
	/// A test-only subclass of <see cref="NpgsqlException"/> that allows setting the
	/// <see cref="DbException.SqlState"/> property, which is read-only on the base class.
	/// </summary>
	private sealed class TestNpgsqlException(string? sqlState)
		: NpgsqlException($"Test exception (SqlState: {sqlState})")
	{
		/// <inheritdoc/>
		public override string? SqlState => sqlState;
	}
}
