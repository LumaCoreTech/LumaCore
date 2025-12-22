// Copyright (c) 2025 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using System.Collections;
using System.Reflection;

using LumaCore.Api.Configuration;

namespace LumaCore.Api.Features.System;

/// <summary>
/// Provides methods to sanitize Options objects for safe diagnostic output.
/// </summary>
/// <remarks>
///     <para>
///     This class converts Options objects to dictionaries while masking any properties
///     marked with <see cref="SecretAttribute"/>. It is designed for use in diagnostic
///     endpoints where configuration values need to be exposed without leaking secrets.
///     </para>
///     <para>
///     The sanitizer handles:
///     <list type="bullet">
///         <item>Simple properties (strings, numbers, booleans)</item>
///         <item>Nested objects (recursively sanitized)</item>
///         <item>Collections (arrays, lists)</item>
///         <item>Null values</item>
///     </list>
///     </para>
/// </remarks>
static class OptionsSanitizer
{
	/// <summary>
	/// The placeholder value used to mask secrets in output.
	/// </summary>
	private const string MaskedValue = "***";

	/// <summary>
	/// Sanitizes an Options object by masking secret properties.
	/// </summary>
	/// <typeparam name="T">The type of the Options object.</typeparam>
	/// <param name="options">The Options object to sanitize.</param>
	/// <returns>
	/// A dictionary containing the sanitized property values, with secrets masked.
	/// </returns>
	/// <remarks>
	/// Properties marked with <see cref="SecretAttribute"/> are replaced with a masked
	/// representation. By default, the mask includes the length of the original value.
	/// </remarks>
	public static IDictionary<string, object?> Sanitize<T>(T options)
		where T : class
	{
		return Sanitize(options, typeof(T));
	}

	/// <summary>
	/// Sanitizes an Options object by masking secret properties.
	/// </summary>
	/// <param name="options">The Options object to sanitize.</param>
	/// <param name="optionsType">The type of the Options object.</param>
	/// <returns>
	/// A dictionary containing the sanitized property values, with secrets masked.
	/// Properties are sorted alphabetically.
	/// </returns>
	public static IDictionary<string, object?> Sanitize(object options, Type optionsType)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(optionsType);

		// Use SortedDictionary for alphabetical ordering of properties.
		var result = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

		// Iterate all public instance properties of the Options type.
		foreach (PropertyInfo property in optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			// Skip indexers (e.g., this[int index]) and write-only properties.
			if (property.GetIndexParameters().Length > 0 || !property.CanRead)
				continue;

			object? value = property.GetValue(options);
			var secretAttr = property.GetCustomAttribute<SecretAttribute>();

			if (secretAttr is not null)
			{
				// Property is marked as secret — mask its value.
				result[property.Name] = MaskSecret(value, secretAttr.ShowLength);
			}
			else if (value is null)
			{
				// Preserve null values as-is.
				result[property.Name] = null;
			}
			else if (IsSimpleType(property.PropertyType))
			{
				// Simple types (primitives, strings, etc.) — use value directly.
				result[property.Name] = value;
			}
			else if (value is IEnumerable enumerable and not string)
			{
				// Collections — sanitize each element recursively.
				result[property.Name] = SanitizeEnumerable(enumerable, property.PropertyType);
			}
			else
			{
				// Nested complex object — recurse into its properties.
				result[property.Name] = Sanitize(value, property.PropertyType);
			}
		}

		return result;
	}

	/// <summary>
	/// Masks a secret value for safe output.
	/// </summary>
	/// <param name="value">The secret value to mask.</param>
	/// <param name="showLength">Whether to include the length in the masked output.</param>
	/// <returns>
	/// <see langword="null"/> if <paramref name="value"/> is <see langword="null"/>;<br/>
	/// <c>***</c> if <paramref name="showLength"/> is <see langword="false"/>,
	/// or <c>*** (length N)</c> if <paramref name="showLength"/> is <see langword="true"/>.
	/// </returns>
	private static string? MaskSecret(object? value, bool showLength)
	{
		// Null values remain null.
		if (value is null)
			return null;

		// If length display is disabled, return simple mask.
		if (!showLength)
			return MaskedValue;

		// Determine length based on value type.
		int length = value switch
		{
			string s      => s.Length,
			ICollection c => c.Count,
			IEnumerable e => e.Cast<object>().Count(),
			var _         => value.ToString()?.Length ?? 0
		};

		return $"{MaskedValue} (length {length})";
	}

	/// <summary>
	/// Determines whether a type is a simple type that should be serialized directly.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns>
	/// <see langword="true"/> if the type is a primitive, string, enum, or common value type;
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Simple types are passed through without recursion. This includes:
	/// primitives, enums, strings, decimals, DateTime, DateTimeOffset, TimeSpan, Guid, and Uri.
	/// Nullable versions of these types are also considered simple.
	/// </remarks>
	private static bool IsSimpleType(Type type)
	{
		// Unwrap Nullable<T> to get the underlying type.
		Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

		return underlyingType.IsPrimitive
		       || underlyingType.IsEnum
		       || underlyingType == typeof(string)
		       || underlyingType == typeof(decimal)
		       || underlyingType == typeof(DateTime)
		       || underlyingType == typeof(DateTimeOffset)
		       || underlyingType == typeof(TimeSpan)
		       || underlyingType == typeof(Guid)
		       || underlyingType == typeof(Uri);
	}

	/// <summary>
	/// Sanitizes an enumerable collection, recursively sanitizing complex elements.
	/// </summary>
	/// <param name="enumerable">The collection to sanitize.</param>
	/// <param name="propertyType">The declared type of the property (used to determine element type).</param>
	/// <returns>A list of sanitized elements.</returns>
	private static List<object?> SanitizeEnumerable(IEnumerable enumerable, Type propertyType)
	{
		// Try to determine the element type from the collection's generic argument.
		Type? elementType = GetElementType(propertyType);
		var list = new List<object?>();

		foreach (object? item in enumerable)
		{
			if (item is null)
			{
				list.Add(null);
			}
			else if (elementType is not null && IsSimpleType(elementType))
			{
				// Simple element type — add directly.
				list.Add(item);
			}
			else if (item is string s)
			{
				// Strings are simple even if elementType couldn't be determined.
				list.Add(s);
			}
			else
			{
				// Complex element — recurse into its properties.
				list.Add(Sanitize(item, item.GetType()));
			}
		}

		return list;
	}

	/// <summary>
	/// Gets the element type of an enumerable type.
	/// </summary>
	/// <param name="enumerableType">The enumerable type (e.g., <c>List&lt;string&gt;</c>).</param>
	/// <returns>
	/// The element type (e.g., <c>string</c>), or <see langword="null"/> if it cannot be determined.
	/// </returns>
	private static Type? GetElementType(Type enumerableType)
	{
		// Arrays have a dedicated method.
		if (enumerableType.IsArray)
			return enumerableType.GetElementType();

		// Generic collections (List<T>, IEnumerable<T>, etc.) — extract T.
		if (enumerableType.IsGenericType)
			return enumerableType.GetGenericArguments().FirstOrDefault();

		// Non-generic collections (ArrayList, etc.) — element type unknown.
		return null;
	}
}
