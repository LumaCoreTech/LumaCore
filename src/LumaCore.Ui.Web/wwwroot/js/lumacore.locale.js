/**
 * @fileoverview LumaCore Locale Initialization
 * @description Sets the HTML lang attribute from localStorage before rendering.
 * @version 1.0.0
 * @license MIT
 *
 * @remarks This file MUST be loaded early in <head> to set the lang attribute
 *          before any content renders (for accessibility/SEO).
 */

document.documentElement.lang = localStorage.getItem('locale') || 'en';
