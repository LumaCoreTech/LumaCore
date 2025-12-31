/**
 * @fileoverview LumaCore Toast Notifications
 * @description Temporary notification messages that auto-dismiss.
 * @version 1.0.0
 * @license MIT
 *
 * @remarks Requires lumacore.icons.js for theme-aware icon loading.
 */

// ============================================
// TOAST NOTIFICATIONS
// ============================================

/**
 * Shows a toast notification message.
 *
 * @description Creates and displays a temporary notification that automatically
 *              dismisses after the specified duration. Multiple toasts stack vertically.
 *              The toast container is created on demand and removed when empty.
 *              Icons are loaded from the current theme with fallback to default.
 *
 * @param {string} message - The message text to display.
 * @param {'success'|'error'|'info'|'warning'} [type='success'] - The toast type, determines icon and styling.
 * @param {number} [duration=3000] - Time in milliseconds before auto-dismiss.
 * @returns {void}
 *
 * @example
 * // Success toast (default)
 * window.showToast('Settings saved!');
 *
 * // Error toast with longer duration
 * window.showToast('Connection failed', 'error', 5000);
 *
 * // Info toast
 * window.showToast('Tip: Press ESC to close', 'info');
 */
window.showToast = function(message, type = 'success', duration = 3000) {
    // Get or create toast container
    let container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'lc-toast-container';
        document.body.appendChild(container);
    }

    // Create toast element
    const toast = document.createElement('div');
    toast.className = `lc-toast lc-toast-${type}`;

    // Create icon container (will be filled async)
    const iconSpan = document.createElement('span');
    iconSpan.className = 'lc-toast-icon';

    // Create message span
    const messageSpan = document.createElement('span');
    messageSpan.className = 'lc-toast-message';
    messageSpan.textContent = message;

    toast.appendChild(iconSpan);
    toast.appendChild(messageSpan);
    container.appendChild(toast);

    // Load icon asynchronously (with theme fallback)
    const iconName = `toast-${type}`;
    if (window.getIcon) {
        window.getIcon(iconName).then(svg => {
            if (svg) {
                iconSpan.innerHTML = svg;
            }
        });
    }

    // Trigger enter animation on next frame
    requestAnimationFrame(() => {
        toast.classList.add('lc-toast-show');
    });

    // Schedule auto-dismiss
    setTimeout(() => {
            // Trigger exit animation
            toast.classList.remove('lc-toast-show');
            toast.classList.add('lc-toast-hide');

            // Remove from DOM after animation completes
            setTimeout(() => {
                    container.removeChild(toast);

                    // Clean up container if empty
                    if (container.children.length === 0) {
                        document.body.removeChild(container);
                    }
                },
                300); // Match CSS transition duration
        },
        duration);
};
