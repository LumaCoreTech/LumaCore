/**
 * @fileoverview LanguageSwitcher Dropdown Handler
 * @description Collocated JavaScript module for LanguageSwitcher component.
 *              Handles outside clicks and ESC key to close the dropdown.
 */

/** @type {function(MouseEvent): void|null} */
let clickHandler = null;

/** @type {function(KeyboardEvent): void|null} */
let escHandler = null;

/** @type {Object|null} */
let dotnetRef = null;

/**
 * Registers handlers for closing the dropdown.
 *
 * @param {Object} ref - Reference to the LanguageSwitcher Blazor component.
 * @param {HTMLElement} element - The dropdown container element.
 */
export function registerDropdownHandlers(ref, element) {
    // Clean up any existing handlers first
    unregisterDropdownHandlers();

    dotnetRef = ref;

    // Handle clicks outside the dropdown
    clickHandler = function(e) {
        if (element && !element.contains(e.target)) {
            dotnetRef.invokeMethodAsync('CloseDropdown');
        }
    };

    // Handle ESC key
    escHandler = function(e) {
        if (e.key === 'Escape') {
            e.preventDefault();
            dotnetRef.invokeMethodAsync('CloseDropdown');
        }
    };

    // Use setTimeout to avoid catching the click that opened the dropdown
    setTimeout(() => {
            document.addEventListener('click', clickHandler);
            document.addEventListener('keydown', escHandler);
        },
        0);
}

/**
 * Unregisters the dropdown handlers.
 */
export function unregisterDropdownHandlers() {
    if (clickHandler) {
        document.removeEventListener('click', clickHandler);
        clickHandler = null;
    }
    if (escHandler) {
        document.removeEventListener('keydown', escHandler);
        escHandler = null;
    }
    dotnetRef = null;
}
