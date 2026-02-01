// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Core;

/// <summary>
/// Represents the context passed to lifecycle operations such as initialization, shutdown, and disposal.
/// </summary>
/// <remarks>
///     <para>
///     This interface provides a type-safe way to pass contextual information through the lifecycle phases of an object.
///     Derived classes can implement this interface to provide strongly-typed access to context-specific data, while
///     still allowing access to arbitrary key-value pairs via the <see cref="Items"/> dictionary.
///     </para>
///     <para>
///     Example implementation:
///     </para>
///     <code>
///     public class MyServiceContext : ILifecycleContext
///     {
///         public IDictionary&lt;string, object&gt; Items { get; } = new Dictionary&lt;string, object&gt;();
///         public required IConfiguration Configuration { get; init; }
///         public required IServiceProvider Services { get; init; }
///     }
///     </code>
/// </remarks>
public interface ILifecycleContext
{
	/// <summary>
	/// Gets a dictionary that can be used to store arbitrary key-value pairs during lifecycle operations.
	/// </summary>
	/// <value>
	/// A dictionary for storing additional context data that does not have a strongly-typed property.
	/// </value>
	/// <remarks>
	/// Use this for ad-hoc data that needs to be passed between lifecycle phases. For frequently used data,
	/// consider adding strongly-typed properties to the implementing class instead.
	/// </remarks>
	IDictionary<string, object> Items { get; }
}

/// <summary>
/// Default implementation of <see cref="ILifecycleContext"/> that provides a simple property bag.
/// </summary>
/// <remarks>
/// Use this class when you don't need custom strongly-typed properties in your lifecycle context.
/// For more complex scenarios, implement <see cref="ILifecycleContext"/> directly.
/// </remarks>
public sealed class LifecycleContext : ILifecycleContext
{
	/// <inheritdoc/>
	public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
}
