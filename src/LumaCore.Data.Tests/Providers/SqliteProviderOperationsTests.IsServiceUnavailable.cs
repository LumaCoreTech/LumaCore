// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net.Sockets;

using LumaCore.Data.Providers;

using Microsoft.Data.Sqlite;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqliteProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenServiceUnavailable_ReturnsTrue"/>.
	/// Covers SQLite error codes, generic exception types, and nested exception trees.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsTrue_TestData() => new()
	{
		// SQLite error code: SQLITE_PERM (3) — access permission denied
		{
			"SQLITE_PERM (3)",
			CreateSqliteException(3)
		},

		// SQLite error code: SQLITE_BUSY (5) — database locked
		{
			"SQLITE_BUSY (5)",
			CreateSqliteException(5)
		},

		// SQLite error code: SQLITE_LOCKED (6) — table locked (same connection deadlock)
		{
			"SQLITE_LOCKED (6)",
			CreateSqliteException(6)
		},

		// SQLite error code: SQLITE_NOMEM (7) — memory allocation failed
		{
			"SQLITE_NOMEM (7)",
			CreateSqliteException(7)
		},

		// SQLite error code: SQLITE_READONLY (8) — read-only database
		{
			"SQLITE_READONLY (8)",
			CreateSqliteException(8)
		},

		// SQLite error code: SQLITE_IOERR (10) — disk I/O error
		{
			"SQLITE_IOERR (10)",
			CreateSqliteException(10)
		},

		// SQLite error code: SQLITE_CORRUPT (11) — database disk image is malformed
		{
			"SQLITE_CORRUPT (11)",
			CreateSqliteException(11)
		},

		// SQLite error code: SQLITE_FULL (13) — disk full
		{
			"SQLITE_FULL (13)",
			CreateSqliteException(13)
		},

		// SQLite error code: SQLITE_CANTOPEN (14) — unable to open database file
		{
			"SQLITE_CANTOPEN (14)",
			CreateSqliteException(14)
		},

		// SQLite error code: SQLITE_PROTOCOL (15) — database lock protocol error
		{
			"SQLITE_PROTOCOL (15)",
			CreateSqliteException(15)
		},

		// SQLite error code: SQLITE_NOTADB (26) — file is not a database
		{
			"SQLITE_NOTADB (26)",
			CreateSqliteException(26)
		},

		// SQLite extended error code: SQLITE_IOERR_READ (266 = 10 | (1 << 8)).
		// Verifies that the primary code mask (errorCode & 0xFF) correctly extracts SQLITE_IOERR (10).
		{
			"SQLITE_IOERR_READ (266, extended)",
			CreateSqliteException(266)
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

		// Nested: SqliteException wrapped in another exception
		{
			"Nested SqliteException",
			new InvalidOperationException("Wrapper", CreateSqliteException(14))
		},

		// Nested: TimeoutException inside AggregateException
		{
			"TimeoutException in AggregateException",
			new AggregateException(new TimeoutException("Timeout"))
		},

		// Deeply nested: AggregateException → InvalidOperationException → SqliteException (3 levels).
		// Verifies that depth-first traversal follows InnerException chains inside AggregateException children.
		{
			"Deeply nested SqliteException (3 levels)",
			new AggregateException(new InvalidOperationException("Middle", CreateSqliteException(14)))
		},

		// Mixed AggregateException: infrastructure error alongside cancellation.
		// The infrastructure error must not be masked by the OperationCanceledException.
		{
			"AggregateException with SocketException + OperationCanceledException",
			new AggregateException(
				new SocketException((int)SocketError.ConnectionRefused),
				new OperationCanceledException("User cancelled"))
		},

		// Mixed AggregateException: cancellation first, infrastructure error second.
		// Verifies traversal order does not affect the result.
		{
			"AggregateException with OperationCanceledException + SqliteException",
			new AggregateException(
				new OperationCanceledException("User cancelled"),
				CreateSqliteException(5))
		}
	};

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.IsServiceUnavailable"/> returns <see langword="true"/>
	/// for exceptions that indicate the database is unreachable or corrupted.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="exception">The exception to check.</param>
	[Theory]
	[MemberData(nameof(IsServiceUnavailable_ReturnsTrue_TestData))]
	public void IsServiceUnavailable_WhenServiceUnavailable_ReturnsTrue(string scenario, Exception exception)
	{
		_ = scenario;

		// Arrange
		var sut = new SqliteProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenNotServiceUnavailable_ReturnsFalse"/>.
	/// Covers user cancellations, non-connection SQLite errors, and generic exceptions.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsFalse_TestData() => new()
	{
		// OperationCanceledException — user-initiated, not infrastructure failure
		{
			"OperationCanceledException",
			new OperationCanceledException("User cancelled")
		},

		// TaskCanceledException — derived from OperationCanceledException
		{
			"TaskCanceledException",
			new TaskCanceledException("Task cancelled")
		},

		// Nested OperationCanceledException — wrapped by provider
		{
			"Nested OperationCanceledException",
			new InvalidOperationException("Wrapper", new OperationCanceledException())
		},

		// SQLite error code: SQLITE_ERROR (1) — generic SQL error (syntax, etc.)
		{
			"SQLITE_ERROR (1) — non-connection",
			CreateSqliteException(1)
		},

		// SQLite error code: SQLITE_CONSTRAINT (19) — constraint violation
		{
			"SQLITE_CONSTRAINT (19)",
			CreateSqliteException(19)
		},

		// Generic exception — not connection-related
		{
			"ArgumentException",
			new ArgumentException("Bad argument")
		}
	};

	/// <summary>
	/// Verifies that <see cref="SqliteProviderOperations.IsServiceUnavailable"/> returns <see langword="false"/>
	/// for exceptions that do not indicate service unavailability.
	/// </summary>
	/// <param name="scenario">A description of the test scenario for readability in test output.</param>
	/// <param name="exception">The exception to check.</param>
	[Theory]
	[MemberData(nameof(IsServiceUnavailable_ReturnsFalse_TestData))]
	public void IsServiceUnavailable_WhenNotServiceUnavailable_ReturnsFalse(string scenario, Exception exception)
	{
		_ = scenario;

		// Arrange
		var sut = new SqliteProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Creates a <see cref="SqliteException"/> with the specified SQLite error code.
	/// </summary>
	/// <param name="errorCode">The SQLite error code.</param>
	/// <returns>A <see cref="SqliteException"/> with the specified error code.</returns>
	private static SqliteException CreateSqliteException(int errorCode)
	{
		// SqliteException requires a message and error code.
		// The constructor is: SqliteException(string message, int errorCode)
		return new SqliteException($"SQLite Error {errorCode}", errorCode);
	}
}
