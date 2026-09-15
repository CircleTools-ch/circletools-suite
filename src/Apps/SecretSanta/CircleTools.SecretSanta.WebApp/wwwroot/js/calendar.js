// Hands an iCalendar (.ics) document to the OS so the browser routes it to the calendar app (MNF-6,
// ADR-0028 amendment). A Blob download is the portable path: desktop browsers open it in the calendar
// app, mobile browsers offer "add to calendar". Nothing is written silently — the calendar UI confirms.
export function addToCalendar(filename, ics) {
    const blob = new Blob([ics], { type: 'text/calendar;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    // Release the object URL after the click has been handled.
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// The web can always produce the .ics file, so the action is always offered.
export function isSupported() {
    return true;
}
