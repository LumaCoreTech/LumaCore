/**
 * @fileoverview LumaCore Icon System
 * @description Theme-aware icon loading with fallback support.
 * @version 1.0.0
 * @license MIT
 *
 * @remarks Icons are loaded as inline SVG to support currentColor inheritance.
 *          Each theme can override icons by placing them in its icons/ folder.
 *          Missing icons fall back to the default theme (lumacore-dark).
 */

// ============================================
// CONFIGURATION
// ============================================

/**
 * Theme used as fallback when an icon is not found in the current theme.
 * @type {string}
 */
const FALLBACK_THEME = 'lumacore-base';

// ============================================
// ICON CACHE
// ============================================

/**
 * Cache for loaded icon SVGs.
 * Key format: "themeId/iconName" → SVG string
 * @type {Map<string, string>}
 */
const iconCache = new Map();

/**
 * Cache for failed icon lookups (to avoid repeated 404s).
 * @type {Set<string>}
 */
const failedLookups = new Set();

// ============================================
// ICON LOADING
// ============================================

/**
 * Gets an icon SVG string, with theme fallback support.
 *
 * @description Attempts to load the icon from the current theme's icons folder.
 *              If not found, falls back to the default theme (lumacore-dark).
 *              Results are cached to avoid repeated network requests.
 *
 * @param {string} iconName - The icon name without extension (e.g., "toast-success").
 * @param {string} [themeId] - The theme to load from. Defaults to current theme.
 * @returns {Promise<string>} The SVG markup string, or empty string if not found.
 *
 * @example
 * const svg = await window.getIcon('toast-success');
 * element.innerHTML = svg;
 */
window.getIcon = async function(iconName, themeId) {
    // Use current theme if not specified
    const theme = themeId || window.getThemeId?.() || FALLBACK_THEME;
    const cacheKey = `${theme}/${iconName}`;

    // Check cache first
    if (iconCache.has(cacheKey)) {
        return iconCache.get(cacheKey);
    }

    // Try to load from current theme
    let svg = await fetchIcon(theme, iconName);

    // Fallback to default theme if not found and not already using default
    if (!svg && theme !== FALLBACK_THEME) {
        const fallbackKey = `${FALLBACK_THEME}/${iconName}`;

        if (iconCache.has(fallbackKey)) {
            return iconCache.get(fallbackKey);
        }

        svg = await fetchIcon(FALLBACK_THEME, iconName);

        if (svg) {
            iconCache.set(fallbackKey, svg);
        }
    }

    // Cache the result (even if from fallback, cache under original key too)
    if (svg) {
        iconCache.set(cacheKey, svg);
    }

    return svg || '';
};

/**
 * Preloads a set of icons for faster access.
 *
 * @description Use this to preload icons that will be needed soon,
 *              avoiding visible loading delays.
 *
 * @param {string[]} iconNames - Array of icon names to preload.
 * @param {string} [themeId] - The theme to preload from. Defaults to current theme.
 * @returns {Promise<void>}
 *
 * @example
 * // Preload toast icons on app start
 * await window.preloadIcons(['toast-success', 'toast-error', 'toast-info', 'toast-warning']);
 */
window.preloadIcons = async function(iconNames, themeId) {
    const promises = iconNames.map(name => window.getIcon(name, themeId));
    await Promise.all(promises);
};

/**
 * Clears the icon cache.
 *
 * @description Call this when the theme changes to ensure icons are
 *              reloaded from the new theme.
 *
 * @returns {void}
 */
window.clearIconCache = function() {
    iconCache.clear();
    failedLookups.clear();
};

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
 */
function resolveUrl(relativePath) {
    return new URL(relativePath, document.baseURI).href;
}

// ============================================
// INTERNAL HELPERS
// ============================================

/**
 * Fetches an icon SVG from a specific theme.
 *
 * @param {string} themeId - The theme ID.
 * @param {string} iconName - The icon name.
 * @returns {Promise<string|null>} The SVG string, or null if not found.
 */
async function fetchIcon(themeId, iconName) {
    const path = resolveUrl(`themes/${themeId}/icons/${iconName}.svg`);

    // Skip if we already know this path doesn't exist
    if (failedLookups.has(path)) {
        return null;
    }

    try {
        const response = await fetch(path);

        if (!response.ok) {
            failedLookups.add(path);
            return null;
        }

        return await response.text();
    } catch (error) {
        failedLookups.add(path);
        return null;
    }
}
