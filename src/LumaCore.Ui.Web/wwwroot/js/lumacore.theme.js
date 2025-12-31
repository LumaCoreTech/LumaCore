/**
 * @fileoverview LumaCore Theme System
 * @description Theme discovery, switching, and preview functionality.
 * @version 1.0.0
 * @license MIT
 *
 * @remarks This file MUST be loaded BEFORE Blazor to ensure the saved theme is applied
 *          before the first render, preventing flash of unstyled content (FOUC).
 */

// ============================================
// TYPE DEFINITIONS
// ============================================

/**
 * Theme metadata as defined in theme.json files.
 * @typedef {Object} ThemeMetadata
 * @property {string} id - Unique theme identifier (e.g., "lumacore-dark").
 * @property {Object<string, string>} name - Localized theme names keyed by locale code.
 * @property {Object<string, string>} [description] - Localized theme descriptions.
 * @property {string} category - Theme category (e.g., "official", "community").
 * @property {Object<string, string>} [categoryLabel] - Localized category labels.
 * @property {string} icon - Icon filename (e.g., "icon.svg").
 * @property {string} author - Theme author name.
 * @property {string} version - Theme version string.
 * @property {number} order - Display order for sorting.
 * @property {ThemeColors} colors - Theme color definitions.
 */

/**
 * Theme color definitions used for visual identification.
 * @typedef {Object} ThemeColors
 * @property {PreviewColors} preview - Colors for the gradient stripe in theme selector cards.
 */

/**
 * Colors for the theme card gradient stripe.
 * @typedef {Object} PreviewColors
 * @property {string} background - Primary background color (66% of gradient).
 * @property {string} accent - Accent color (33% of gradient).
 */

/**
 * Theme manifest structure as defined in themes/manifest.json.
 * @typedef {Object} ThemeManifest
 * @property {ThemeManifestEntry[]} themes - Array of theme entries.
 */

/**
 * Single entry in the theme manifest.
 * @typedef {Object} ThemeManifestEntry
 * @property {string} id - Unique theme identifier matching the folder name.
 * @property {number} order - Display order for sorting in the theme selector.
 */

// ============================================
// URL RESOLUTION
// ============================================

/**
 * Resolves a relative path against the document's base URI.
 *
 * @description Ensures paths work correctly when the app is hosted under a sub-path
 *              (e.g., /ui/) by resolving relative to the &lt;base href&gt; tag.
 *
 * @param {string} relativePath - Path relative to app root (e.g., "themes/manifest.json").
 * @returns {string} Fully resolved URL.
 *
 * @example
 * // If base href is "/ui/":
 * resolveUrl("themes/manifest.json") // => "https://example.com/ui/themes/manifest.json"
 */
function resolveUrl(relativePath) {
    return new URL(relativePath, document.baseURI).href;
}

// ============================================
// THEME DISCOVERY
// ============================================

/**
 * Cache of discovered theme metadata.
 * @type {ThemeMetadata[]}
 */
let availableThemes = [];

/**
 * Discovers available themes by loading the theme manifest.
 *
 * @description Fetches the theme manifest file to get the list of available themes,
 *              then loads each theme's metadata from its theme.json file.
 *              Themes that fail to load are silently skipped.
 *
 * @returns {Promise<ThemeMetadata[]>} Array of theme metadata objects, sorted by order.
 *
 * @example
 * const themes = await discoverThemes();
 * console.log(themes[0].id); // "lumacore-dark"
 */
async function discoverThemes() {
    const themes = [];

    try {
        // Load theme manifest
        const manifestResponse = await fetch(resolveUrl('themes/manifest.json'));
        if (!manifestResponse.ok) {
            console.error('[Theme] Failed to load theme manifest');
            return themes;
        }

        const manifest = await manifestResponse.json();

        // Load metadata for each theme in manifest
        for (const entry of manifest.themes) {
            try {
                const response = await fetch(resolveUrl(`themes/${entry.id}/theme.json`));
                if (response.ok) {
                    const themeMetadata = await response.json();
                    themes.push(themeMetadata);
                }
            } catch (_) {
                // Skip themes that fail to load — non-critical error
            }
        }
    } catch (error) {
        console.error('[Theme] Error discovering themes:', error);
    }

    // Sort by order property for consistent display
    themes.sort((a, b) => a.order - b.order);
    availableThemes = themes;

    return themes;
}

/**
 * Gets all available themes, discovering them if not already cached.
 *
 * @description This function is called from Blazor via JS interop to populate
 *              the theme selector. Results are cached after first discovery.
 *
 * @returns {Promise<ThemeMetadata[]>} Array of available theme metadata objects.
 *
 * @example
 * // From Blazor:
 * var themes = await JsRuntime.InvokeAsync<JsonElement>("getAvailableThemes");
 */
window.getAvailableThemes = async function() {
    if (availableThemes.length === 0) {
        await discoverThemes();
    }
    return availableThemes;
};

/**
 * Gets a specific theme by its ID.
 *
 * @param {string} themeId - The unique theme identifier to look up.
 * @returns {Promise<ThemeMetadata|undefined>} The theme metadata, or undefined if not found.
 *
 * @example
 * const theme = await window.getThemeById('missi-pink');
 * if (theme) {
 *     console.log(theme.author); // "Missi"
 * }
 */
window.getThemeById = async function(themeId) {
    if (availableThemes.length === 0) {
        await discoverThemes();
    }
    return availableThemes.find(t => t.id === themeId);
};

// ============================================
// THEME SWITCHER
// ============================================

/**
 * Currently active theme ID.
 * @type {string}
 */
let currentTheme = localStorage.getItem('theme') || 'lumacore-dark';

/**
 * Theme ID currently being previewed (on hover), or null if not previewing.
 * @type {string|null}
 */
let previewingTheme = null;

/**
 * Initializes the theme system on page load.
 *
 * @description Immediately-invoked function that applies the saved theme
 *              before Blazor renders, preventing flash of default theme.
 *              This runs synchronously during script parsing.
 */
(function initTheme() {
    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `themes/${currentTheme}/theme.css`;
    }
})();

/**
 * Gets the currently active theme ID.
 *
 * @returns {string} The current theme ID (e.g., "lumacore-dark").
 *
 * @example
 * // From Blazor:
 * var themeId = await JsRuntime.InvokeAsync<string>("getThemeId");
 */
window.getThemeId = function() {
    return currentTheme;
};

/**
 * Previews a theme temporarily without persisting the selection.
 *
 * @description Called on mouse hover over theme cards. Applies the theme
 *              CSS immediately for live preview. The preview can be reset
 *              by calling {@link resetThemePreview}.
 *
 * @param {string} themeId - The theme ID to preview.
 * @returns {void}
 *
 * @example
 * // On mouseenter:
 * window.previewTheme('missi-pink');
 * // On mouseleave:
 * window.resetThemePreview();
 */
window.previewTheme = function(themeId) {
    // Don't preview if already showing this theme
    if (themeId === currentTheme) {
        return;
    }

    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        previewingTheme = themeId;
        themeLink.href = `themes/${themeId}/theme.css`;
    }
};

/**
 * Resets the theme preview back to the current saved theme.
 *
 * @description Called on mouse leave from theme cards or when closing
 *              the settings overlay. Has no effect if not currently previewing.
 *
 * @returns {void}
 */
window.resetThemePreview = function() {
    if (!previewingTheme) {
        return;
    }

    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `themes/${currentTheme}/theme.css`;
        previewingTheme = null;
    }
};

/**
 * Selects and persists a theme permanently.
 *
 * @description Called on click/selection of a theme card. Saves the theme
 *              to localStorage, applies it immediately, and shows a success toast.
 *
 * @param {string} themeId - The theme ID to select and persist.
 * @param {string} [toastMessage] - Optional localized message for the success toast.
 * @returns {void}
 *
 * @example
 * // From Blazor on theme click (toastMessage from localization):
 * string toastMessage = L.Get("components.settings.theme.toasts.saved");
 * await JsRuntime.InvokeVoidAsync("selectTheme", "ocean-blue", toastMessage);
 */
window.selectTheme = function(themeId, toastMessage) {
    currentTheme = themeId;
    previewingTheme = null;

    // Persist to localStorage (survives page reload, cleared on logout)
    localStorage.setItem('theme', themeId);

    // Clear icon cache so new theme icons are loaded
    if (window.clearIconCache) {
        window.clearIconCache();
    }

    // Apply theme CSS
    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `themes/${themeId}/theme.css`;

        // Show confirmation toast (requires lumacore.toast.js)
        if (window.showToast && toastMessage) {
            window.showToast(toastMessage, 'success');
        }
    }
};

/**
 * Resets the theme to the system default.
 *
 * @description Called during logout to clear user's theme preference and
 *              restore the default theme. Removes the theme from localStorage.
 *
 * @returns {void}
 *
 * @example
 * // From Blazor logout handler:
 * await JsRuntime.InvokeVoidAsync("resetThemeToDefault");
 */
window.resetThemeToDefault = function() {
    const systemDefault = 'lumacore-dark';

    currentTheme = systemDefault;
    previewingTheme = null;

    // Clear persisted theme preference
    localStorage.removeItem('theme');

    // Clear icon cache so default theme icons are loaded
    if (window.clearIconCache) {
        window.clearIconCache();
    }

    // Apply system default theme
    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `themes/${systemDefault}/theme.css`;
    }
};
