// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

// Keeps <html lang> on the language the UI is showing (BTP-7). Screen readers pick their voice from it and
// browsers use it to decide whether to offer a translation. A module, because the Content-Security-Policy
// rules out eval, which is the only way to set it from .NET without a script of our own.
export function setLanguage(language) {
    document.documentElement.lang = language;
}
