// Connectivity bridge for BrowserConnectivity (ADR-0044): exposes navigator.onLine and the window
// online/offline events to .NET so the silent auto-sync can flush queued edits on reconnect. Defined
// here (an external file) rather than inline because the Content-Security-Policy forbids inline
// scripts. Registered before Blazor.start so the global exists before any .NET call reaches for it.
window.circletoolsConnectivity = {
    isOnline: function () { return navigator.onLine; },
    register: function (dotNetRef) {
        const notify = function () {
            dotNetRef.invokeMethodAsync('OnConnectivityChanged', navigator.onLine);
        };
        window.addEventListener('online', notify);
        window.addEventListener('offline', notify);
    }
};

// Blazor startup, in a file rather than an inline <script> so the two web heads start the runtime
// the same way (see Main's js/app.js, where a Content-Security-Policy makes it mandatory).

// loadAllSatelliteResources downloads the resource assemblies for *every* culture the app ships,
// not just the one it starts in.
//
// Without it, switching language at runtime silently falls back to the neutral strings: the runtime
// only fetches satellite assemblies for the startup culture, so a French UI has no French resources
// to read. The visible symptom is a picker that "works" and produces the wrong language — worse
// than an obvious failure. This app used to reload the page on every change, which hid the problem
// by re-bootstrapping in the new culture; since the migration onto the shared CultureBoundary
// (ADR-0025) it re-renders instead, so the resources have to be here already.
// New-version prompt (ADR-0048). The offline service worker (registered below) parks a new deploy in
// the "waiting" state until every tab closes; this bridges its update lifecycle to .NET so the app can
// offer a "Reload" prompt, and reloads once the user accepts and the new worker takes control. Defined
// here (external file) because the Content-Security-Policy forbids inline scripts.
window.circletoolsUpdate = {
    _reg: null,
    _reloading: false,

    // Called from .NET (UpdatePrompt) with a reference that receives OnUpdateAvailable().
    register: function (dotNetRef) {
        if (!('serviceWorker' in navigator)) {
            return;
        }

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
                dotNetRef.invokeMethodAsync('OnUpdateAvailable');
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
                        dotNetRef.invokeMethodAsync('OnUpdateAvailable');
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
