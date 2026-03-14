// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LumaCore.Data;

/// <summary>
/// EF Core value converter that normalizes <see cref="DateTime"/> values to <see cref="DateTimeKind.Utc"/>
/// on both read and write.
/// </summary>
/// <remarks>
///     <para>
///     This converter acts as a defense-in-depth measure for multi-provider compatibility. PostgreSQL's
///     Npgsql provider (6+) maps <see cref="DateTime"/> properties to <c>timestamp with time zone</c> and
///     rejects values with <see cref="DateTimeKind.Unspecified"/> at parameter binding time. Other providers
///     (SQLite, SQL Server) are lenient about <see cref="DateTimeKind"/>, so bugs only surface on PostgreSQL.
///     </para>
///     <para>
///     The converter calls <see cref="DateTime.SpecifyKind"/> to stamp the <see cref="DateTimeKind.Utc"/>
///     flag <b>without altering the tick value</b>. This is safe because every <see cref="DateTime"/>
///     property in this model represents a UTC timestamp (by naming convention: <c>*Utc</c> suffix).
///     </para>
///     <para>
///     Registered globally via <see cref="LumaCoreDbContext.ConfigureConventions"/> for all
///     <see cref="DateTime"/> properties. EF Core automatically lifts the converter for
///     <see cref="Nullable{T}">DateTime?</see> properties.
///     </para>
/// </remarks>
sealed class UtcDateTimeConverter()
	: ValueConverter<DateTime, DateTime>(
		v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
		v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
