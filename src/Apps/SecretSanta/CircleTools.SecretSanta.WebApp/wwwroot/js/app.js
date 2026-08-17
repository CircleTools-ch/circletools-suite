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
Blazor.start({
    configureRuntime: (builder) => builder.withConfig({ loadAllSatelliteResources: true }),
}).then(() => {
    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
});
