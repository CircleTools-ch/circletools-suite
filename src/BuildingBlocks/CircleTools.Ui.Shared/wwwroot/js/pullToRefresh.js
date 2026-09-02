// Shared pull-to-refresh gesture (FEAT-008 W3, ADR-0054). A WebView/browser touch gesture used by both
// the WASM web heads and the MAUI Blazor Hybrid head (both render in a web view). When the page is
// scrolled to the very top and the user drags down past a threshold, it invokes a .NET callback (which
// the shell wires to the background sync) and shows a brief spinner. Self-contained, no dependency.
//
// The gesture only engages while the page is at the top AND the finger is moving down, and only then
// calls preventDefault — so ordinary scrolling is never blocked. A single active wiring at a time.

let handlers = null;

export function attach(dotNetRef) {
    detach(); // idempotent: a re-attach replaces any previous wiring

    const indicator = document.getElementById('ct-ptr-indicator');
    if (!indicator) {
        return;
    }
    const spinner = indicator.querySelector('.ct-ptr-spinner');

    const THRESHOLD = 72; // px of (resisted) pull needed to trigger a refresh
    const MAX = 120;      // px the indicator can travel
    const RESIST = 0.5;   // drag-distance → travel ratio (rubber-band feel)

    let startY = 0;
    let dragging = false;
    let pulling = false;
    let pull = 0;

    const atTop = () => (window.scrollY || document.documentElement.scrollTop || 0) <= 0;

    const paint = (y, progress) => {
        indicator.style.transform = `translateY(${y}px)`;
        indicator.style.opacity = `${Math.min(progress, 1)}`;
        if (spinner) {
            spinner.style.transform = `rotate(${progress * 270}deg)`;
        }
        indicator.classList.toggle('ct-ptr-ready', progress >= 1);
    };

    const reset = () => {
        indicator.classList.remove('ct-ptr-refreshing', 'ct-ptr-ready');
        indicator.style.transition = 'transform .2s ease, opacity .2s ease';
        indicator.style.transform = '';
        indicator.style.opacity = '0';
        if (spinner) {
            spinner.style.transform = '';
        }
        window.setTimeout(() => { indicator.style.transition = ''; }, 220);
    };

    const onStart = (e) => {
        if (e.touches.length !== 1 || !atTop()) {
            return;
        }
        startY = e.touches[0].clientY;
        dragging = true;
        pulling = false;
        pull = 0;
        indicator.style.transition = '';
    };

    const onMove = (e) => {
        if (!dragging) {
            return;
        }
        const dy = e.touches[0].clientY - startY;
        if (dy <= 0 || !atTop()) {
            // Moving up, or the content has scrolled away from the top: hand the gesture back.
            if (pulling) {
                reset();
            }
            dragging = false;
            pulling = false;
            return;
        }
        pulling = true;
        e.preventDefault(); // own the gesture; stop native overscroll while pulling
        pull = Math.min(dy * RESIST, MAX);
        paint(pull, pull / THRESHOLD);
    };

    const onEnd = () => {
        if (!dragging) {
            return;
        }
        dragging = false;
        if (pulling && pull >= THRESHOLD) {
            indicator.style.transition = 'transform .2s ease';
            indicator.style.transform = `translateY(${THRESHOLD}px)`;
            indicator.classList.add('ct-ptr-refreshing');
            dotNetRef.invokeMethodAsync('OnPulled');
            window.setTimeout(reset, 700); // brief acknowledgement; data flows in via the sync's own re-render
        } else if (pulling) {
            reset();
        }
        pulling = false;
    };

    window.addEventListener('touchstart', onStart, { passive: true });
    window.addEventListener('touchmove', onMove, { passive: false });
    window.addEventListener('touchend', onEnd, { passive: true });
    window.addEventListener('touchcancel', onEnd, { passive: true });

    handlers = { onStart, onMove, onEnd };
}

export function detach() {
    if (!handlers) {
        return;
    }
    window.removeEventListener('touchstart', handlers.onStart);
    window.removeEventListener('touchmove', handlers.onMove);
    window.removeEventListener('touchend', handlers.onEnd);
    window.removeEventListener('touchcancel', handlers.onEnd);
    handlers = null;
}
