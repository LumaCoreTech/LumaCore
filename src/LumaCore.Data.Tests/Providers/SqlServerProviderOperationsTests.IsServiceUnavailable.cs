// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Net.Sockets;
using System.Reflection;

using LumaCore.Data.Providers;

using Microsoft.Data.SqlClient;

using Xunit;

namespace LumaCore.Data.Tests.Providers;

public sealed partial class SqlServerProviderOperationsTests
{
	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenServiceUnavailable_ReturnsTrue"/>.
	/// Covers SQL Server error numbers, generic exception types, and nested exception trees.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsTrue_TestData() => new()
	{
		// SQL Server: Timeout expired (-2)
		{
			"Timeout expired (-2)",
			CreateSqlException(-2)
		},

		// SQL Server: Connection error (-1)
		{
			"Connection error (-1)",
			CreateSqlException(-1)
		},

		// SQL Server: Named pipe connection error (53)
		{
			"Named pipe error (53)",
			CreateSqlException(53)
		},

		// SQL Server: Out of memory (701)
		{
			"Out of memory (701)",
			CreateSqlException(701)
		},

		// SQL Server: Database offline (942)
		{
			"Database offline (942)",
			CreateSqlException(942)
		},

		// SQL Server: SQL Server paused (17142)
		{
			"SQL Server paused (17142)",
			CreateSqlException(17142)
		},

		// SQL Server: Azure SQL busy (40501)
		{
			"Azure SQL busy (40501)",
			CreateSqlException(40501)
		},

		// SQL Server: Azure SQL not available (40613)
		{
			"Azure SQL not available (40613)",
			CreateSqlException(40613)
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

		// Nested: SqlException wrapped in another exception
		{
			"Nested SqlException",
			new InvalidOperationException("Wrapper", CreateSqlException(-2))
		},

		// Nested: TimeoutException inside AggregateException
		{
			"TimeoutException in AggregateException",
			new AggregateException(new TimeoutException("Connection timed out"))
		},

		// Deeply nested: AggregateException → InvalidOperationException → SqlException (3 levels).
		// Verifies that depth-first traversal follows InnerException chains inside AggregateException children.
		{
			"Deeply nested SqlException (3 levels)",
			new AggregateException(new InvalidOperationException("Middle", CreateSqlException(-2)))
		},

		// Mixed AggregateException: infrastructure error alongside cancellation.
		// The infrastructure error must not be masked by the OperationCanceledException.
		{
			"AggregateException with SqlException + OperationCanceledException",
			new AggregateException(
				CreateSqlException(-2),
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
	/// Verifies that <see cref="SqlServerProviderOperations.IsServiceUnavailable"/> returns
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
		var sut = new SqlServerProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.True(result);
	}

	/// <summary>
	/// Test data for <see cref="IsServiceUnavailable_WhenNotServiceUnavailable_ReturnsFalse"/>.
	/// Covers user cancellations, non-connection SQL Server errors, and generic exceptions.
	/// </summary>
	public static TheoryData<string, Exception> IsServiceUnavailable_ReturnsFalse_TestData() => new()
	{
		// OperationCanceledException — user-initiated, not infrastructure failure
		{
			"OperationCanceledException",
			new OperationCanceledException("User cancelled")
		},

		// Nested OperationCanceledException
		{
			"Nested OperationCanceledException",
			new InvalidOperationException("Wrapper", new OperationCanceledException())
		},

		// SQL Server: Error 547 (constraint violation) — not connection-related
		{
			"Constraint violation (547)",
			CreateSqlException(547)
		},

		// SQL Server: Error 2627 (unique key violation) — not connection-related
		{
			"Unique key violation (2627)",
			CreateSqlException(2627)
		},

		// Generic exception — not connection-related
		{
			"ArgumentException",
			new ArgumentException("Bad argument")
		}
	};

	/// <summary>
	/// Verifies that <see cref="SqlServerProviderOperations.IsServiceUnavailable"/> returns
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
		var sut = new SqlServerProviderOperations();

		// Act
		bool result = sut.IsServiceUnavailable(exception);

		// Assert
		Assert.False(result);
	}

	/// <summary>
	/// Creates a <see cref="SqlException"/> with the specified error number using reflection,
	/// because <see cref="SqlException"/> has no public constructors.
	/// </summary>
	/// <param name="errorNumber">The SQL Server error number.</param>
	/// <returns>A <see cref="SqlException"/> with the specified error number.</returns>
	private static SqlException CreateSqlException(int errorNumber)
	{
		// SqlErrorCollection and SqlError have internal constructors that change across versions.
		// Use the factory approach: create a minimal SqlException via internal CreateException().
		SqlErrorCollection collection = CreateErrorCollection();
		SqlError error = CreateError(errorNumber);
		AddErrorToCollection(collection, error);

		// SqlException.CreateException(SqlErrorCollection, string serverVersion)
		MethodInfo createException = typeof(SqlException)
			.GetMethod(
				"CreateException",
				BindingFlags.NonPublic | BindingFlags.Static,
				[typeof(SqlErrorCollection), typeof(string)])!;

		return (SqlException)createException.Invoke(null, [collection, "16.0.0"])!;
	}

	/// <summary>
	/// Creates an empty <see cref="SqlErrorCollection"/> via its internal constructor.
	/// </summary>
	private static SqlErrorCollection CreateErrorCollection()
	{
		return (SqlErrorCollection)typeof(SqlErrorCollection)
			.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!
			.Invoke([]);
	}

	/// <summary>
	/// Creates a <see cref="SqlError"/> with the specified error number, trying multiple internal
	/// constructor signatures that vary across <c>Microsoft.Data.SqlClient</c> versions.
	/// </summary>
	/// <param name="errorNumber">The SQL Server error number.</param>
	private static SqlError CreateError(int errorNumber)
	{
		// Try all known constructor signatures (they differ between SqlClient versions).
		ConstructorInfo[] ctors = typeof(SqlError)
			.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);

		foreach (ConstructorInfo ctor in ctors)
		{
			ParameterInfo[] parameters = ctor.GetParameters();

			try
			{
				// Build arguments matching the parameter types
				object?[] args = new object?[parameters.Length];
				for (int i = 0; i < parameters.Length; i++)
				{
					Type pt = parameters[i].ParameterType;
					if (pt == typeof(int))
						args[i] = i == 0 ? errorNumber : 0;
					else if (pt == typeof(byte))
						args[i] = (byte)0;
					else if (pt == typeof(uint))
						args[i] = (uint)0;
					else if (pt == typeof(string))
						args[i] = "";
					else if (pt == typeof(Exception))
						args[i] = null;
					else
						args[i] = null;
				}

				return (SqlError)ctor.Invoke(args);
			}
			catch
			{
				// Try next constructor
			}
		}

		throw new InvalidOperationException($"Could not create SqlError. Available constructors: {ctors.Length}");
	}

	/// <summary>
	/// Adds a <see cref="SqlError"/> to a <see cref="SqlErrorCollection"/> via the internal Add method.
	/// </summary>
	private static void AddErrorToCollection(SqlErrorCollection collection, SqlError error)
	{
		typeof(SqlErrorCollection)
			.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
			.Invoke(collection, [error]);
	}
}
