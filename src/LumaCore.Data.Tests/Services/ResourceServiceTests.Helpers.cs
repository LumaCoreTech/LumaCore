// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Data.Services;
using LumaCore.Data.Tests.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System.Text;

using LumaCore.Core.IO;

namespace LumaCore.Data.Tests.Services;

public sealed partial class ResourceServiceTests
{
	/// <summary>
	/// Creates a <see cref="ResourceService"/> backed by the fixture's <see cref="LumaCoreDbContext"/>
	/// and the supplied store.
	/// </summary>
	/// <param name="fixture">The database fixture providing the context.</param>
	/// <param name="store">The fake resource store to use for file persistence.</param>
	/// <param name="preferCompiledHotPathQueries">
	/// Whether the SUT should opt into pre-compiled hot-path queries for resource lookups.
	/// Defaults to <see langword="false"/> to mirror the production default.
	/// </param>
	/// <param name="logger">An optional logger; defaults to <see cref="NullLogger{T}.Instance"/>.</param>
	/// <param name="timeProvider">
	/// An optional <see cref="TimeProvider"/>; defaults to <see cref="TimeProvider.System"/>. Tests that need to
	/// verify the nullable-<c>utcNow</c> fallback should pass a deterministic clock.
	/// </param>
	/// <returns>A new <see cref="ResourceService"/> ready for tests.</returns>
	private static ResourceService CreateSut(
		DbFixture                 fixture,
		FakeResourceStore         store,
		bool                      preferCompiledHotPathQueries = false,
		ILogger<ResourceService>? logger                       = null,
		TimeProvider?             timeProvider                 = null)
	{
		return new ResourceService(
			fixture.DbContext,
			store,
			streamBufferPool: new StreamBufferPool(new StreamBufferPoolOptions()),
			Options.Create(new DatabaseOptions { PreferCompiledHotPathQueries = preferCompiledHotPathQueries }),
			timeProvider ?? TimeProvider.System,
			logger ?? NullLogger<ResourceService>.Instance);
	}

	/// <summary>
	/// Buffers the supplied UTF-8 string into a <see cref="MemoryStream"/> for upload.
	/// </summary>
	/// <param name="content">The string content to wrap.</param>
	/// <returns>A <see cref="MemoryStream"/> positioned at zero.</returns>
	private static MemoryStream MakeStream(string content)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(content);
		return new MemoryStream(bytes, writable: false);
	}
}
