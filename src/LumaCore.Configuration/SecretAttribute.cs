// Copyright (c) 2025-2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

namespace LumaCore.Configuration;

/// <summary>
/// Marks a property as containing secret data that should be masked in diagnostic output.
/// </summary>
/// <remarks>
///     <para>
///     Apply this attribute to Options properties that contain sensitive information such as
///     API keys, signing keys, passwords, or connection strings. The System feature's diagnostic
///     endpoints will automatically mask these values when serializing options.
///     </para>
///     <para>
///     By default, masked output shows the length of the original value (e.g., <c>*** (length 32)</c>).
///     Set <see cref="ShowLength"/> to <see langword="false"/> to hide the length as well.
///     </para>
/// </remarks>
/// <example>
///     <code>
/// public sealed class JwtOptions
/// {
///     public string Issuer { get; set; } = string.Empty;
///     
///     [Secret]
///     public string SigningKey { get; set; } = string.Empty;
///     
///     [Secret(ShowLength = false)]
///     public string? ApiKey { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SecretAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether the length of the secret should be shown in masked output.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to show the length (e.g., <c>*** (length 32)</c>);
	/// <see langword="false"/> to show only <c>***</c>. The default is <see langword="true"/>.
	/// </value>
	public bool ShowLength { get; init; } = true;
}
