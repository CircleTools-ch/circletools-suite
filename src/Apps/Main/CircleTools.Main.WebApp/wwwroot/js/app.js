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
Blazor.start({
    configureRuntime: (builder) => builder.withConfig({ loadAllSatelliteResources: true }),
}).then(() => {
    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
});
