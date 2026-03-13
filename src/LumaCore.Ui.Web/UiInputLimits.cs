// Copyright (c) 2026 LumaCoreTech
// SPDX-License-Identifier: MIT
// Project: https://github.com/LumaCoreTech/LumaCore

using LumaCore.Definitions;

namespace LumaCore.Ui.Web;

/// <summary>
/// Defines UI-only input limits used to constrain user input at the HTML layer.
/// </summary>
/// <remarks>
/// These limits are intentionally <b>UI-only</b> and must not be treated as business rules or security boundaries.
/// Server-side validation and authentication must not rely on these values.
/// </remarks>
public static class UiInputLimits
{
	/// <summary>
	/// Maximum number of characters allowed in a login username field.
	/// </summary>
	/// <remarks>
	/// This forwards to the shared cross-layer limit (<see cref="LumaCore.Definitions.EntityLimits"/>) to avoid
	/// duplicating the value in the UI.
	/// </remarks>
	public const int LoginUsernameMaxLength = EntityLimits.UsernameMaxLength;

	/// <summary>
	/// Maximum number of characters allowed in a login password field.
	/// </summary>
	/// <remarks>
	/// This limit is intentionally generous and exists primarily to prevent accidental or malicious extremely large
	/// input payloads from being sent from the browser.
	/// </remarks>
	public const int LoginPasswordMaxLength = 1024;
}
