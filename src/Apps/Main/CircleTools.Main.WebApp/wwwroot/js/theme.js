// Colour mode (ADR-0024 dark mode). Sets Bootstrap 5.3's data-bs-theme on <html> from the OS
// preference so every component gets its dark palette, and follows the OS if it changes at runtime.
//
// In a file, not an inline <script>, because the Content-Security-Policy has no 'unsafe-inline'
// (ADR-0021). Loaded from <head> so it runs before the first paint — no flash of the wrong theme.
(function () {
    var query = window.matchMedia('(prefers-color-scheme: dark)');
    function apply() {
        document.documentElement.setAttribute('data-bs-theme', query.matches ? 'dark' : 'light');
    }
    apply();
    query.addEventListener('change', apply);
})();
