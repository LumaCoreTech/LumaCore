// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

namespace LumaCore.Core.Tests;

public partial class ExceptionHelpersTests
{
	/// <summary>
	/// Verifies that <see cref="ExceptionHelpers.PrepareForRethrow"/> rethrows the original exception.
	/// </summary>
	[Fact]
	public void PrepareForRethrow_WhenCalled_RethrowsOriginalException()
	{
		// Arrange
		var originalException = new InvalidOperationException("Test exception");

		// Act + Assert
		var thrownException =
			Assert.Throws<InvalidOperationException>(() => ExceptionHelpers.PrepareForRethrow(originalException));

		Assert.Same(originalException, thrownException);
	}

	/// <summary>
	/// Verifies that <see cref="ExceptionHelpers.PrepareForRethrow"/> preserves the original stack trace.
	/// </summary>
	[Fact]
	public void PrepareForRethrow_WhenCalled_PreservesOriginalStackTrace()
	{
		// Arrange
		Exception? caughtException = null;
		string? originalStackTrace = null;

		try
		{
			ThrowOriginalException();
		}
		catch (InvalidOperationException ex)
		{
			originalStackTrace = ex.StackTrace;
			caughtException = ex;
		}

		// Act + Assert
		var rethrown =
			Assert.Throws<InvalidOperationException>(() => ExceptionHelpers.PrepareForRethrow(caughtException!));

		Assert.NotNull(rethrown.StackTrace);
		Assert.Contains(nameof(ThrowOriginalException), rethrown.StackTrace);
		Assert.Contains(originalStackTrace!, rethrown.StackTrace);
	}

	/// <summary>
	/// Verifies that <see cref="ExceptionHelpers.PrepareForRethrow"/> works correctly when unwrapping
	/// an <see cref="AggregateException"/>.
	/// </summary>
	[Fact]
	public void PrepareForRethrow_WhenUnwrappingAggregateException_PreservesInnerExceptionStackTrace()
	{
		// Arrange
		Exception? innerException = null;

		try
		{
			ThrowOriginalException();
		}
		catch (InvalidOperationException ex)
		{
			innerException = ex;
		}

		var aggregateException = new AggregateException(innerException!);

		// Act + Assert
		var rethrown = Assert.Throws<InvalidOperationException>(() =>
			ExceptionHelpers.PrepareForRethrow(aggregateException.InnerException!));

		Assert.Same(innerException, rethrown);
		Assert.NotNull(rethrown.StackTrace);
		Assert.Contains(nameof(ThrowOriginalException), rethrown.StackTrace);
	}

	/// <summary>
	/// Verifies that <see cref="ExceptionHelpers.PrepareForRethrow"/> can be used with throw keyword pattern.
	/// </summary>
	[Fact]
	public void PrepareForRethrow_WhenUsedWithThrowKeyword_RethrowsException()
	{
		// Arrange
		// ReSharper disable once NotResolvedInText
		var originalException = new ArgumentException("Test argument exception", "testParam");

		// Act + Assert
		var thrownException = Assert.Throws<ArgumentException>(RethrowWithThrowKeyword);

		Assert.Same(originalException, thrownException);

		void RethrowWithThrowKeyword()
		{
			throw ExceptionHelpers.PrepareForRethrow(originalException);
		}
	}

	/// <summary>
	/// Verifies that <see cref="ExceptionHelpers.PrepareForRethrow"/> throws <see cref="ArgumentNullException"/>
	/// when the exception parameter is <see langword="null"/>.
	/// </summary>
	[Fact]
	public void PrepareForRethrow_WhenExceptionIsNull_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act + Assert
		// Note: The ArgumentNullException is thrown by ExceptionDispatchInfo.Capture(), which names its parameter "source"
		var ex = Assert.Throws<ArgumentNullException>(() => ExceptionHelpers.PrepareForRethrow(exception));

		Assert.Equal("source", ex.ParamName);
	}
}
