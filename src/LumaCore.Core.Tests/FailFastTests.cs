// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Xunit;

// ReSharper disable MoveLocalFunctionAfterJumpStatement

namespace LumaCore.Core.Tests;

/// <summary>
/// Unit tests for <see cref="FailFast"/>.
/// </summary>
/// <remarks>
/// Each test runs inside a <see cref="FailFastSubscriptionGuard"/> which removes any
/// <see cref="FailFast.BeforeTermination"/> / <see cref="FailFast.TerminationRequested"/> subscriptions
/// the test installed, so static event subscriptions cannot leak between tests.
/// </remarks>
[Trait("Category", "Core")]
public sealed class FailFastTests
{
	#region TerminateApplication(string)

	/// <summary>
	/// Verifies that <see cref="FailFast.TerminateApplication(string)"/> raises
	/// <see cref="FailFast.BeforeTermination"/> first and <see cref="FailFast.TerminationRequested"/>
	/// second when both events have subscribers and termination is not canceled.
	/// </summary>
	/// <remarks>
	/// The actual <see cref="Environment.FailFast(string)"/> would kill the process; the test handler
	/// throws to abort the call before that happens.
	/// </remarks>
	[Fact]
	public void TerminateApplication_String_WhenBothEventsSubscribed_FiresInOrder()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		var firingOrder = new List<string>();

		void OnBefore(object? _, FailFastEventArgs e) => firingOrder.Add("before");

		void OnTermination(string _, Exception? __)
		{
			firingOrder.Add("termination");

			// Throwing aborts the call before Environment.FailFast — this keeps the test process alive.
			throw new InvalidOperationException("abort termination from test");
		}

		FailFast.BeforeTermination += OnBefore;
		guard.Track(OnBefore);
		FailFast.TerminationRequested += OnTermination;
		guard.Track(OnTermination);

		// Act
		var ex = Assert.Throws<InvalidOperationException>(() => FailFast.TerminateApplication("boom"));

		// Assert
		Assert.Equal("abort termination from test", ex.Message);
		Assert.Equal(["before", "termination"], firingOrder);
	}

	/// <summary>
	/// Verifies that <see cref="FailFast.TerminateApplication(string)"/> can be canceled by setting
	/// <see cref="FailFastEventArgs.Cancel"/> to <see langword="true"/>, that the resulting
	/// <see cref="FailFastCanceledException"/> exposes the original message and a <see langword="null"/>
	/// inner exception, and that <see cref="FailFast.TerminationRequested"/> is not raised in that case.
	/// </summary>
	[Fact]
	public void TerminateApplication_String_WhenCanceled_ThrowsFailFastCanceledExceptionWithMessage()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		bool terminationRequestedFired = false;

		static void Cancel(object?       _, FailFastEventArgs e)  => e.Cancel = true;
		void        OnTermination(string _, Exception?        __) => terminationRequestedFired = true;

		FailFast.BeforeTermination += Cancel;
		guard.Track(Cancel);
		FailFast.TerminationRequested += OnTermination;
		guard.Track(OnTermination);

		// Act
		var ex = Assert.Throws<FailFastCanceledException>(() => FailFast.TerminateApplication("boom"));

		// Assert
		Assert.Equal("boom", ex.Message);
		Assert.Null(ex.InnerException);
		Assert.False(terminationRequestedFired, "TerminationRequested must not fire when Cancel=true.");
	}

	/// <summary>
	/// Verifies that when no <see cref="FailFast.BeforeTermination"/> subscriber is attached, the call
	/// proceeds straight to <see cref="FailFast.TerminationRequested"/>. The handler throws to abort the
	/// call before <see cref="Environment.FailFast(string)"/> kills the test process.
	/// </summary>
	[Fact]
	public void TerminateApplication_String_WhenNoBeforeSubscriber_ProceedsToTerminationRequested()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		bool terminationFired = false;

		void OnTermination(string msg, Exception? _)
		{
			terminationFired = true;
			Assert.Equal("hello", msg);
			throw new InvalidOperationException("abort");
		}

		FailFast.TerminationRequested += OnTermination;
		guard.Track(OnTermination);

		// Act
		Assert.Throws<InvalidOperationException>(() => FailFast.TerminateApplication("hello"));

		// Assert
		Assert.True(terminationFired);
	}

	#endregion

	#region TerminateApplication(Exception)

	/// <summary>
	/// Verifies that the cancellation path of <see cref="FailFast.TerminateApplication(Exception)"/>
	/// surfaces the original exception both to the <see cref="FailFast.BeforeTermination"/> subscriber
	/// (via <see cref="FailFastEventArgs.Exception"/>) and on the resulting
	/// <see cref="FailFastCanceledException"/> (via <see cref="Exception.Message"/> and
	/// <see cref="Exception.InnerException"/>), and that <see cref="FailFast.TerminationRequested"/>
	/// is not raised.
	/// </summary>
	[Fact]
	public void TerminateApplication_Exception_WhenCanceled_PreservesExceptionEverywhere()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		Exception? capturedException = null;
		bool terminationRequestedFired = false;

		void CaptureAndCancel(object? _, FailFastEventArgs e)
		{
			capturedException = e.Exception;
			e.Cancel = true;
		}

		void OnTermination(string _, Exception? __) => terminationRequestedFired = true;

		FailFast.BeforeTermination += CaptureAndCancel;
		guard.Track(CaptureAndCancel);
		FailFast.TerminationRequested += OnTermination;
		guard.Track(OnTermination);

		var inner = new InvalidOperationException("root cause");

		// Act
		var ex = Assert.Throws<FailFastCanceledException>(() => FailFast.TerminateApplication(inner));

		// Assert
		Assert.Same(inner, capturedException);
		Assert.Equal(inner.Message, ex.Message);
		Assert.Same(inner, ex.InnerException);
		Assert.False(terminationRequestedFired, "TerminationRequested must not fire when Cancel=true.");
	}

	/// <summary>
	/// Verifies that <see cref="FailFast.TerminateApplication(Exception)"/> rejects a <see langword="null"/>
	/// exception with <see cref="ArgumentNullException"/> and reports the offending parameter name.
	/// </summary>
	[Fact]
	public void TerminateApplication_Exception_WhenExceptionNull_ThrowsArgumentNullException()
	{
		// Act + Assert
		var ex = Assert.Throws<ArgumentNullException>(() => FailFast.TerminateApplication(exception: null!));
		Assert.Equal("exception", ex.ParamName);
	}

	#endregion

	#region Subscribe / Unsubscribe contract

	/// <summary>
	/// Verifies that an unsubscribed <see cref="FailFast.BeforeTermination"/> handler is no longer invoked.
	/// </summary>
	[Fact]
	public void BeforeTermination_AfterUnsubscribe_HandlerIsNotInvoked()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		bool unsubscribedHandlerInvoked = false;

		void Handler(object? _, FailFastEventArgs e)
		{
			unsubscribedHandlerInvoked = true;
			e.Cancel = true;
		}

		FailFast.BeforeTermination += Handler;
		FailFast.BeforeTermination -= Handler;

		// A separate handler keeps the test process alive by canceling termination.
		static void CancelOnly(object? _, FailFastEventArgs e) => e.Cancel = true;
		FailFast.BeforeTermination += CancelOnly;
		guard.Track(CancelOnly);

		// Act
		Assert.Throws<FailFastCanceledException>(() => FailFast.TerminateApplication("ignored"));

		// Assert
		Assert.False(unsubscribedHandlerInvoked);
	}

	#endregion

	#region Subscriber-vs-lock contract

	/// <summary>
	/// Verifies that a <see cref="FailFast.BeforeTermination"/> handler can subscribe further handlers
	/// while it is being invoked.
	/// </summary>
	[Fact]
	public void TerminateApplication_WhenSubscriberSubscribesAnotherHandler_DoesNotDeadlock()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		bool nestedHandlerInvoked = false;
		bool terminationRequestedFired = false;

		void NestedHandler(object? sender, FailFastEventArgs e)  => nestedHandlerInvoked = true;
		void OnTermination(string  _,      Exception?        __) => terminationRequestedFired = true;

		void OuterHandler(object? sender, FailFastEventArgs e)
		{
			// Re-entrant subscription must succeed even though we are currently invoked from inside a
			// TerminateApplication call. Cancel termination so the test process is not killed.
			FailFast.BeforeTermination += NestedHandler;
			// ReSharper disable once AccessToDisposedClosure
			guard.Track(NestedHandler);

			e.Cancel = true;
		}

		FailFast.BeforeTermination += OuterHandler;
		guard.Track(OuterHandler);
		FailFast.TerminationRequested += OnTermination;
		guard.Track(OnTermination);

		// Act: first call — only OuterHandler is wired, NestedHandler is added during the call but does
		// not observe this call (event delegate snapshot is taken before invocation).
		Assert.Throws<FailFastCanceledException>(() => FailFast.TerminateApplication("first"));

		// Assert (intermediate): NestedHandler did not observe the first call.
		Assert.False(nestedHandlerInvoked, "Nested handler must not observe the call during which it was added.");

		// Act: second call — OuterHandler still cancels, NestedHandler is now part of the snapshot too.
		Assert.Throws<FailFastCanceledException>(() => FailFast.TerminateApplication("second"));

		// Assert
		Assert.True(nestedHandlerInvoked, "Nested handler must observe subsequent calls after subscribing.");
		Assert.False(terminationRequestedFired, "TerminationRequested must not fire when Cancel=true.");
	}

	/// <summary>
	/// Verifies that <see cref="FailFast.TerminationRequested"/>'s <c>+=</c> accessor can be invoked from
	/// within a <see cref="FailFast.BeforeTermination"/> callback without deadlocking on the FailFast
	/// internal lock.
	/// </summary>
	[Fact]
	public void TerminationRequested_AddDuringBeforeTerminationCallback_DoesNotDeadlock()
	{
		// Arrange
		using var guard = new FailFastSubscriptionGuard();
		bool nestedSubscribed = false;

		// Cancel termination so neither this nor any nested handler kills the process.
		static void NestedTermination(string _, Exception? __)
		{
			/* never invoked — Cancel is true */
		}

		void Subscribe(object? _, FailFastEventArgs e)
		{
			FailFast.TerminationRequested += NestedTermination;
			// ReSharper disable once AccessToDisposedClosure
			guard.Track(NestedTermination);
			nestedSubscribed = true;
			e.Cancel = true;
		}

		FailFast.BeforeTermination += Subscribe;
		guard.Track(Subscribe);

		// Act
		Assert.Throws<FailFastCanceledException>(() => FailFast.TerminateApplication("test"));

		// Assert
		Assert.True(nestedSubscribed);
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Removes any <see cref="FailFast"/> subscriptions a test installed, in reverse order of registration,
	/// so static event subscriptions cannot leak between tests.
	/// </summary>
	private sealed class FailFastSubscriptionGuard : IDisposable
	{
		private readonly List<Action> mUnsubscribers = [];

		/// <summary>
		/// Tracks a <see cref="FailFast.BeforeTermination"/> handler for removal on dispose.
		/// </summary>
		/// <param name="handler">The handler that was added to <see cref="FailFast.BeforeTermination"/>.</param>
		public void Track(EventHandler<FailFastEventArgs> handler)
		{
			mUnsubscribers.Add(() => FailFast.BeforeTermination -= handler);
		}

		/// <summary>
		/// Tracks a <see cref="FailFast.TerminationRequested"/> handler for removal on dispose.
		/// </summary>
		/// <param name="handler">The handler that was added to <see cref="FailFast.TerminationRequested"/>.</param>
		public void Track(Action<string, Exception?> handler)
		{
			mUnsubscribers.Add(() => FailFast.TerminationRequested -= handler);
		}

		/// <summary>
		/// Removes all tracked subscriptions in reverse order.
		/// </summary>
		public void Dispose()
		{
			for (int i = mUnsubscribers.Count - 1; i >= 0; i--)
			{
				mUnsubscribers[i].Invoke();
			}
		}
	}

	#endregion
}
