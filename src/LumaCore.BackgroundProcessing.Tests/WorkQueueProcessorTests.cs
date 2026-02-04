// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace LumaCore.BackgroundProcessing.Tests;

/// <summary>
/// Unit tests targeting the <see cref="WorkQueueProcessor"/> class.
/// </summary>
[Trait("Category", "Background Services")]
public partial class WorkQueueProcessorTests
{
	/// <summary>
	/// Gets the logger factory used for tests (<c>NullLoggerFactory</c> that discards all log messages).
	/// </summary>
	private static NullLoggerFactory LoggerFactory => NullLoggerFactory.Instance;
}
