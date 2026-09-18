// Saves generated content as a file (FR-014).
//
// A module rather than inline script, for the same reason as passkeys.js: the CSP has no
// 'unsafe-inline'. The object URL is revoked immediately - the download has already been handed to
// the browser by then, and leaving it alive would pin the whole export in memory for the tab's life.

export function save(fileName, contentType, content) {
    const url = URL.createObjectURL(new Blob([content], { type: contentType }));
    const link = document.createElement('a');

    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    URL.revokeObjectURL(url);
}
