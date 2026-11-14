// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

// Blazor startup, in a file rather than an inline <script> because the Content-Security-Policy has
// no 'unsafe-inline' (ADR-0021). index.html therefore starts the runtime with autostart="false".

// loadAllSatelliteResources downloads the resource assemblies for *every* culture the app ships,
// not just the one it starts in.
//
// Without it, switching language at runtime silently falls back to the neutral (English) strings:
// the runtime only fetches satellite assemblies for the startup culture, so a French UI has no
// French resources to read. The visible symptom is a picker that "works" and produces English —
// worse than an obvious failure. The framework's usual answer is to reload the page on every
// change, which UC-MAIN-005 rules out ("without a reload or a flash of the wrong language"). This
// buys the same result for the cost of two small extra downloads.
// New-version prompt (ADR-0048). The offline service worker (registered below) parks a new deploy in
// the "waiting" state until every tab closes; this bridges its update lifecycle to .NET so the app can
// offer a "Reload" prompt, and reloads once the user accepts and the new worker takes control. Defined
// here (external file) because the Content-Security-Policy forbids inline scripts.
window.circletoolsUpdate = {
    _reg: null,
    _reloading: false,

    _dotNetRef: null,
    _wired: false,

    // Tells whichever UpdatePrompt is mounted now. A language switch rebuilds the component tree, so the
    // reference handed to register() can be disposed and replaced; calling a stale one threw "no tracked
    // object" and a second registration showed the prompt twice (2026-10-15).
    _notify: function () {
        if (this._dotNetRef) {
            this._dotNetRef.invokeMethodAsync('OnUpdateAvailable').catch(() => { });
        }
    },

    // Called from .NET (UpdatePrompt) with a reference that receives OnUpdateAvailable(). Every call
    // replaces the reference; the service-worker listeners are attached only once.
    register: function (dotNetRef) {
        if (!('serviceWorker' in navigator)) {
            return;
        }

        this._dotNetRef = dotNetRef;
        if (this._wired) {
            if (this._reg && this._reg.waiting && navigator.serviceWorker.controller) {
                this._notify();
            }
            return;
        }
        this._wired = true;

        // When the newly activated worker takes control, reload once to run the new assets.
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (this._reloading) {
                return;
            }
            this._reloading = true;
            window.location.reload();
        });

        // Attach to the registration app.js already created (do not register twice).
        navigator.serviceWorker.ready.then(reg => {
            this._reg = reg;

            // Already waiting (installed while the app was closed, or by another tab).
            if (reg.waiting && navigator.serviceWorker.controller) {
                this._notify();
            }

            // A new worker found now: notify once it finishes installing. The controller check keeps the
            // very first install (no previous version) from prompting.
            reg.addEventListener('updatefound', () => {
                const installing = reg.installing;
                if (!installing) {
                    return;
                }
                installing.addEventListener('statechange', () => {
                    if (installing.state === 'installed' && navigator.serviceWorker.controller) {
                        this._notify();
                    }
                });
            });
        });

        // Long-lived tabs: re-check for a new deploy when the tab regains focus, so the prompt can
        // appear without a manual refresh.
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible' && this._reg) {
                this._reg.update().catch(() => { });
            }
        });
    },

    // Called from the "Reload" action: tell the waiting worker to activate; controllerchange reloads.
    apply: function () {
        if (this._reg && this._reg.waiting) {
            this._reg.waiting.postMessage({ type: 'SKIP_WAITING' });
        } else {
            window.location.reload();
        }
    }
};

Blazor.start({
    configureRuntime: (builder) => builder.withConfig({ loadAllSatelliteResources: true }),
}).then(() => {
    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
});
