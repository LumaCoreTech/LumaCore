// LumaCore Theme Switcher & Overlay Handlers
// This file must be loaded BEFORE Blazor!

console.log('[LumaCore] JavaScript loaded successfully!');

// ============================================
// THEME SWITCHER
// ============================================

// Theme system state
let currentTheme = localStorage.getItem('theme') || 'lumacore-dark';
let previewingTheme = null;

// Initialize: Load saved theme on page load
(function initTheme() {
    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `css/themes/${currentTheme}.css`;
        console.log('[Theme] Initialized with theme:', currentTheme);
    } else {
        console.error('[Theme] Theme stylesheet link not found!');
    }
})();

// Get current theme from localStorage
window.getTheme = function() {
    console.log('[Theme] Getting current theme:', currentTheme);
    return currentTheme;
};

// Preview theme on hover - LIVE PREVIEW! ✨
window.previewTheme = function(themeName) {
    console.log('[Theme] Preview request:', themeName, '(current:', currentTheme + ')');
    if (themeName === currentTheme) {
        console.log('[Theme] Already showing this theme, skipping preview');
        return;
    }

    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        previewingTheme = themeName;
        themeLink.href = `css/themes/${themeName}.css`;
        console.log('[Theme] ✨ Preview applied!', themeName);
    } else {
        console.error('[Theme] Theme stylesheet link not found!');
    }
};

// Reset preview - back to current theme
window.resetThemePreview = function() {
    console.log('[Theme] Reset preview (was previewing:', previewingTheme + ')');
    if (!previewingTheme) {
        console.log('[Theme] Not previewing, nothing to reset');
        return;
    }

    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `css/themes/${currentTheme}.css`;
        console.log('[Theme] ✅ Reset to:', currentTheme);
        previewingTheme = null;
    }
};

// Select theme permanently (on click)
window.selectTheme = function(themeName) {
    console.log('[Theme] 💾 Selecting theme:', themeName);
    currentTheme = themeName;
    previewingTheme = null;

    // Save to localStorage (cleared on logout)
    localStorage.setItem('theme', themeName);

    // Apply theme (already showing from preview or set it)
    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `css/themes/${themeName}.css`;
        console.log('[Theme] ✅ Theme saved and applied:', themeName);
    }
};

// Reset theme to system default (on logout)
window.resetThemeToDefault = function() {
    const systemDefault = 'lumacore-dark'; // TODO: Load from appsettings.json
    console.log('[Theme] 🔄 Resetting to system default on logout');

    currentTheme = systemDefault;
    previewingTheme = null;

    // Clear theme localStorage on logout (back to system default)
    localStorage.removeItem('theme');

    // Apply system default theme
    const themeLink = document.getElementById('theme-stylesheet');
    if (themeLink) {
        themeLink.href = `css/themes/${systemDefault}.css`;
        console.log('[Theme] ✅ Reset to system default:', systemDefault);
    }
};

// ============================================
// ADMIN OVERLAY ESC HANDLER
// ============================================

let adminOverlayRef = null;
let escKeyHandler = null;

// Register ESC key handler for admin overlay
window.registerAdminEscHandler = function(dotnetRef) {
    adminOverlayRef = dotnetRef;
    console.log('[Admin] ESC handler registered');

    // Create and register ESC key event listener
    escKeyHandler = function(e) {
        if (e.key === 'Escape' && adminOverlayRef) {
            console.log('[Admin] ESC pressed, closing admin overlay');
            e.preventDefault();
            adminOverlayRef.invokeMethodAsync('OnEscapeKeyPressed');
        }
    };

    document.addEventListener('keydown', escKeyHandler);
};

// Unregister ESC key handler
window.unregisterAdminEscHandler = function() {
    console.log('[Admin] ESC handler unregistered');
    if (escKeyHandler) {
        document.removeEventListener('keydown', escKeyHandler);
        escKeyHandler = null;
    }
    adminOverlayRef = null;
};

// ============================================
// SETTINGS OVERLAY ESC HANDLER
// ============================================

let settingsOverlayRef = null;
let settingsEscKeyHandler = null;

// Register ESC key handler for settings overlay
window.registerSettingsEscHandler = function(dotnetRef) {
    settingsOverlayRef = dotnetRef;
    console.log('[Settings] ESC handler registered');

    // Create and register ESC key event listener
    settingsEscKeyHandler = function(e) {
        if (e.key === 'Escape' && settingsOverlayRef) {
            console.log('[Settings] ESC pressed, closing settings overlay');
            e.preventDefault();
            settingsOverlayRef.invokeMethodAsync('OnEscapeKeyPressed');
        }
    };

    document.addEventListener('keydown', settingsEscKeyHandler);
};

// Unregister ESC key handler
window.unregisterSettingsEscHandler = function() {
    console.log('[Settings] ESC handler unregistered');
    if (settingsEscKeyHandler) {
        document.removeEventListener('keydown', settingsEscKeyHandler);
        settingsEscKeyHandler = null;
    }
    settingsOverlayRef = null;
};

console.log('[LumaCore] All functions registered successfully!');
