// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.Extensions.Logging;

namespace LumaCore.TestUtilities.Logging;

/// <summary>
/// A single log entry captured by <see cref="ListLogger{T}"/> or <see cref="ListLoggerFactory"/>.
/// </summary>
/// <param name="Level">The level at which the entry was emitted.</param>
/// <param name="Message">The fully-formatted message (template + arguments expanded).</param>
/// <param name="Exception">The exception attached to the entry, or <see langword="null"/> if none.</param>
/// <param name="Category">
/// The logger category name (typically the fully-qualified type name of the category type), or
/// <see langword="null"/> when the entry was produced by a sink that does not track categories. Entries
/// created by <see cref="ListLogger{T}"/> have <c>Category = typeof(T).FullName</c>; entries created via
/// <see cref="ListLoggerFactory"/> carry the category name passed to
/// <see cref="ListLoggerFactory.CreateLogger"/>.
/// </param>
public sealed record LogEntry(
	LogLevel   Level,
	string     Message,
	Exception? Exception,
	string?    Category = null);
