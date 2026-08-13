// WebAuthn ceremonies (FR-010, FR-011).
//
// The server hands out options and verifies the result; this file only walks the browser through
// the prompt. It is a module loaded from disk rather than inline script, because the CSP forbids
// inline script and the passkey flow is precisely what that CSP is protecting (ADR-0021).
//
// Options and results cross the wire in WebAuthn's own JSON serialization, which the browser can
// parse and produce itself. Doing the ArrayBuffer/base64url conversion by hand instead is a
// well-known source of subtly wrong credential ids, so it is left to the platform.

function requireSupport() {
    if (!window.PublicKeyCredential
        || typeof PublicKeyCredential.parseCreationOptionsFromJSON !== 'function'
        || typeof PublicKeyCredential.parseRequestOptionsFromJSON !== 'function') {
        throw new Error('This browser does not support passkeys.');
    }
}

// A dismissed prompt is an ordinary choice, not a failure: FR-011 requires that cancelling shows no
// error. The browser reports it as NotAllowedError (and AbortError when something else takes over
// the ceremony), which is why those two alone map to null instead of throwing.
function isCancellation(error) {
    return error && (error.name === 'NotAllowedError' || error.name === 'AbortError');
}

// Password managers that take over the ceremony by shimming navigator.credentials (1Password's
// extension among them) return a toJSON() payload without clientExtensionResults, where the
// platform implementations always emit it — empty object included. The member is required by the
// server's credential model, so its absence fails deserialization before any WebAuthn check runs.
// getClientExtensionResults() is implemented by those shims, so ask it directly; an empty set is
// the honest answer when nothing reports extensions.
function toCredentialJson(credential) {
    const json = credential.toJSON();

    if (json.clientExtensionResults) {
        return json;
    }

    let results;

    try {
        results = credential.getClientExtensionResults?.();
    } catch {
        // Not implemented either. Fall through to the empty set below.
    }

    return { ...json, clientExtensionResults: results ?? {} };
}

export async function create(optionsJson) {
    requireSupport();

    const options = PublicKeyCredential.parseCreationOptionsFromJSON(JSON.parse(optionsJson));

    try {
        const credential = await navigator.credentials.create({ publicKey: options });
        return credential ? JSON.stringify(toCredentialJson(credential)) : null;
    } catch (error) {
        if (isCancellation(error)) {
            return null;
        }

        throw error;
    }
}

export async function assert(optionsJson) {
    requireSupport();

    const options = PublicKeyCredential.parseRequestOptionsFromJSON(JSON.parse(optionsJson));

    try {
        const credential = await navigator.credentials.get({ publicKey: options });
        return credential ? JSON.stringify(toCredentialJson(credential)) : null;
    } catch (error) {
        if (isCancellation(error)) {
            return null;
        }

        throw error;
    }
}

export function isSupported() {
    return !!window.PublicKeyCredential
        && typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function';
}
