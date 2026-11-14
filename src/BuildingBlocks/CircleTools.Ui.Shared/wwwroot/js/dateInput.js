// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

// Native <input type="date"> fields (see .ct-date-field, used for the event date) are styled with
// appearance:none so the browser's picker-indicator chevron — which Android WebView renders as native
// chrome that CSS cannot hide on its own — is gone, in favour of a Material calendar adornment. That
// same appearance:none suppresses the WebView's built-in tap-to-open, so re-add it here: a delegated
// click handler opens the OS date picker from the user's tap. showPicker() must run inside a user
// gesture, which a click handler satisfies; a tap anywhere in the field (text or the calendar icon)
// opens it. Older engines without showPicker fall back to focusing the field (harmless no-op there).
document.addEventListener('click', function (event) {
    var control = event.target.closest && event.target.closest('.ct-date-field');
    if (!control) {
        return;
    }

    var input = control.querySelector('input[type="date"]');
    if (!input) {
        return;
    }

    if (typeof input.showPicker === 'function') {
        try {
            input.showPicker();
        } catch (_) {
            // showPicker throws if not allowed in this context; leaving the field focused is fine.
        }
    }
});
