// Keyboard and scroll behaviour for the phone heads (Q11, 2026-10-16 device test). Shared by the WASM web heads
// and the MAUI Blazor Hybrid head; harmless on desktop.
//
// - While a text field has focus (the soft keyboard is up) <body> carries `ct-typing`, which hides the bottom
//   navigation so it does not ride on top of the keyboard.
// - The focused field is brought into view below the app bar once the keyboard has opened (the page has
//   shrunk), instead of sitting behind the app bar.
// - When the keyboard closes, a page left scrolled past its new end, or a layout viewport left panned, is put
//   back, so the breadcrumb is not stuck under the app bar.
// - A newly opened page starts at the top; Back / Forward restore where that page was.

let wired = false;
let typingTimer = 0;
let popped = false;
const positions = new Map();

const editable = (el) =>
    !!el && (el.isContentEditable
        || el.tagName === 'TEXTAREA'
        || (el.tagName === 'INPUT' && !['checkbox', 'radio', 'button', 'submit', 'range', 'color', 'file'].includes(el.type)));

const appBarHeight = () => {
    const bar = document.querySelector('.mud-appbar');
    return bar ? bar.getBoundingClientRect().bottom : 0;
};

function revealFocused() {
    const el = document.activeElement;
    if (!editable(el)) {
        return;
    }
    const rect = el.getBoundingClientRect();
    const top = appBarHeight() + 8;
    const bottom = (window.visualViewport ? window.visualViewport.height : window.innerHeight) - 8;
    if (rect.top < top || rect.bottom > bottom) {
        el.scrollIntoView({ block: 'center', inline: 'nearest' });
    }
}

function onFocusIn(e) {
    if (!editable(e.target)) {
        return;
    }
    clearTimeout(typingTimer);
    document.body.classList.add('ct-typing');
    // The keyboard animates in and the WebView shrinks over ~300 ms; check again once it has.
    setTimeout(revealFocused, 350);
}

function onFocusOut() {
    clearTimeout(typingTimer);
    typingTimer = setTimeout(() => {
        if (editable(document.activeElement)) {
            return; // focus moved to another field: the keyboard stays up
        }
        document.body.classList.remove('ct-typing');
        settle();
    }, 150);
}

// After the keyboard closed: undo a pan of the layout viewport and clamp a scroll past the page's end.
function settle() {
    setTimeout(() => {
        const vv = window.visualViewport;
        if (vv && vv.offsetTop > 0) {
            window.scrollTo(window.scrollX, Math.max(0, window.scrollY - vv.offsetTop));
        }
        const max = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
        if (window.scrollY > max) {
            window.scrollTo(window.scrollX, max);
        }
    }, 350);
}

function onResize() {
    if (document.body.classList.contains('ct-typing')) {
        revealFocused();
    }
}

export function attach() {
    if (wired) {
        return;
    }
    wired = true;
    if ('scrollRestoration' in history) {
        history.scrollRestoration = 'manual'; // this module restores positions itself
    }
    document.addEventListener('focusin', onFocusIn);
    document.addEventListener('focusout', onFocusOut);
    window.addEventListener('resize', onResize);
    window.addEventListener('popstate', () => { popped = true; });
}

// Called before Blazor renders a new location: remember where the page being left was.
export function leaving(url) {
    positions.set(url, window.scrollY);
}

// Called after the new location rendered: Back / Forward restore, any other navigation starts at the top.
export function arrived(url) {
    const restore = popped && positions.has(url);
    popped = false;
    const y = restore ? positions.get(url) : 0;
    // Twice: once now, once after the page's async load has had a frame to grow the content.
    window.scrollTo(0, y);
    requestAnimationFrame(() => setTimeout(() => window.scrollTo(0, y), 50));
}
