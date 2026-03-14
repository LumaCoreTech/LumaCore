// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net.Sockets;

using LumaCore.Data.Providers;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class MySqlProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenServiceUnavailable_ReturnsTrue"/>.
	/// Covers MySQL error numbers (via reflection-based detection), generic exception types,
	/// and nested exception trees.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsTrue_TestData() => new()
	{
		// MySQL: ER_CON_COUNT_ERROR (1040) — too many connections
		{
			"Too many connections (1040)",
			new MySqlException(1040)
		},

		// MySQL: ER_SERVER_SHUTDOWN (1053) — server shutdown
		{
			"Server shutdown (1053)",
			new MySqlException(1053)
		},

		// MySQL: CR_SERVER_GONE_ERROR (2006) — server has gone away
		{
			"Server gone away (2006)",
			new MySqlException(2006)
		},

		// MySQL: CR_SERVER_LOST (2013) — lost connection during query
		{
			"Lost connection (2013)",
			new MySqlException(2013)
		},

		// MySQL: CR_CONN_HOST_ERROR (2003) — can't connect to server
		{
			"Can't connect (2003)",
			new MySqlException(2003)
		},

		// MySQL: ER_DISK_FULL (1021)
		{
			"Disk full (1021)",
			new MySqlException(1021)
		},

		// MySQL: ER_SERVER_OFFLINE_MODE (3168)
		{
			"Server offline (3168)",
			new MySqlException(3168)
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

		// Nested: MySqlException wrapped in another exception
		{
			"Nested MySqlException",
			new InvalidOperationException("Wrapper", new MySqlException(2006))
		},

		// Nested: SocketException inside AggregateException
		{
			"SocketException in AggregateException",
			new AggregateException(new SocketException((int)SocketError.HostUnreachable))
		},

		// Deeply nested: AggregateException → InvalidOperationException → MySqlException (3 levels).
		// Verifies that depth-first traversal follows InnerException chains inside AggregateException children.
		{
			"Deeply nested MySqlException (3 levels)",
			new AggregateException(new InvalidOperationException("Middle", new MySqlException(2006)))
		},

		// Mixed AggregateException: infrastructure error alongside cancellation.
		// The infrastructure error must not be masked by the OperationCanceledException.
		{
			"AggregateException with MySqlException + OperationCanceledException",
			new AggregateException(
				new MySqlException(2006),
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
	/// Verifies that <see cref="MySqlProviderOperations.IsServiceUnavailable"/> returns
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
		var sut = new MySqlProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenNotServiceUnavailable_ReturnsFalse"/>.
	/// Covers user cancellations, non-connection MySQL errors, and generic exceptions.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsFalse_TestData() => new()
	{
		// OperationCanceledException — user-initiated
		{
			"OperationCanceledException",
			new OperationCanceledException("User cancelled")
		},

		// Nested OperationCanceledException
		{
			"Nested OperationCanceledException",
			new InvalidOperationException("Wrapper", new OperationCanceledException())
		},

		// MySQL: ER_DUP_ENTRY (1062) — duplicate entry, not connection-related
		{
			"Duplicate entry (1062)",
			new MySqlException(1062)
		},

		// MySQL: ER_PARSE_ERROR (1064) — syntax error, not connection-related
		{
			"Syntax error (1064)",
			new MySqlException(1064)
		},

		// Generic exception — not connection-related
		{
			"ArgumentException",
			new ArgumentException("Bad argument")
		}
	};

	/// <summary>
	/// Verifies that <see cref="MySqlProviderOperations.IsServiceUnavailable"/> returns
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
		var sut = new MySqlProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// A fake exception that mimics a real <c>MySqlConnector.MySqlException</c> by having the type name
	/// <c>MySqlException</c> and a public <c>Number</c> property. The production code detects MySQL exceptions
	/// by type name (<c>current.GetType().Name == "MySqlException"</c>) and reads <c>Number</c> via reflection
	/// because the Pomelo MySQL provider is not directly referenced.
	/// </summary>
	/// <remarks>
	/// The class name <b>must</b> be exactly <c>MySqlException</c> for the type name check to match.
	/// </remarks>
	// ReSharper disable once InconsistentNaming — name must match production type name check
	private sealed class MySqlException(int number) : Exception($"MySQL Error {number}")
	{
		/// <summary>
		/// Gets the MySQL error number, matching <c>MySqlConnector.MySqlException.Number</c>.
		/// </summary>
		public int Number { get; } = number;
	}
}
