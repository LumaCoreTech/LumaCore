/**
 * @fileoverview SettingsOverlay ESC Key Handler
 * @description Collocated JavaScript module for SettingsOverlay component.
 */

/** @type {function(KeyboardEvent): void|null} */
let escKeyHandler = null;

/**
 * Registers the ESC key handler for the settings overlay.
 *
 * @param {Object} dotnetRef - Reference to the SettingsOverlay Blazor component.
 */
export function registerEscHandler(dotnetRef) {
    escKeyHandler = function(e) {
        if (e.key === 'Escape') {
            e.preventDefault();
            dotnetRef.invokeMethodAsync('OnEscapeKeyPressed');
        }
    };

    document.addEventListener('keydown', escKeyHandler);
}

/**
 * Unregisters the ESC key handler.
 */
export function unregisterEscHandler() {
    if (escKeyHandler) {
        document.removeEventListener('keydown', escKeyHandler);
        escKeyHandler = null;
    }
}
