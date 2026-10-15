// Presents the native share sheet via the Web Share API (ADR-0028, IShareService).
//
// A module rather than inline script, for the same reason as passkeys.js / download.js: the CSP has
// no 'unsafe-inline'. navigator.share requires transient user activation, so the caller warms this
// module up front (via canShare) and invokes share() straight from the click handler.

export function canShare() {
    return typeof navigator !== 'undefined' && typeof navigator.share === 'function';
}

export async function share(title, text, url) {
    const data = {};
    if (title) data.title = title;
    if (text) data.text = text;
    if (url) data.url = url;

    try {
        await navigator.share(data);
        return true;
    } catch (e) {
        // The user dismissing the sheet rejects with AbortError; that is a normal outcome, not a
        // failure to report - but nothing was shared. Anything else (a genuine share error) bubbles to
        // the .NET caller.
        if (e && e.name === 'AbortError') {
            return false;
        }

        throw e;
    }
}
