// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LumaCore.Data;

/// <summary>
/// Provides an EF Core model cache key that also includes the active database provider.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="LumaCoreDbContext"/> builds a provider-specific filtered unique index for <c>Users.Email</c>.
///     EF Core's default model cache key reuses one model across providers, which would leak the first provider's
///     filter SQL into later contexts created for another provider in the same process.
///     </para>
///     <para>
///     Extending the cache key with <see cref="DbContext.Database"/>'s provider name keeps one cached model per
///     provider while preserving EF Core's design-time/runtime split.
///     </para>
/// </remarks>
sealed class DatabaseProviderModelCacheKeyFactory : IModelCacheKeyFactory
{
	/// <summary>
	/// Creates a model cache key for the specified context.
	/// </summary>
	/// <param name="context">The context whose model is being cached.</param>
	/// <param name="designTime"><see langword="true"/> when the model is built for design-time tooling.</param>
	/// <returns>A cache key that includes the context type, provider name, and design-time flag.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
	public object Create(DbContext context, bool designTime)
	{
		ArgumentNullException.ThrowIfNull(context);

		return (context.GetType(), context.Database.ProviderName, designTime);
	}
}
