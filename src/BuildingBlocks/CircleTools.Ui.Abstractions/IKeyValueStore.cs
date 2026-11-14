// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Abstractions;

/// <summary>
/// How long a stored value is meant to survive. The host decides what each scope maps to —
/// in a browser, <c>sessionStorage</c> and <c>localStorage</c>; on a device, an in-memory
/// dictionary and secure storage.
/// </summary>
public enum StorageScope
{
    /// <summary>Survives a reload, but not the end of the browsing session.</summary>
    Session,

    /// <summary>Survives a restart, until explicitly removed.</summary>
    Durable,
}

/// <summary>
/// A minimal string key/value store, scoped by lifetime.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reads and removes never throw.</strong> Private-mode browsing and blocked cookies both
/// make <c>localStorage</c> inaccessible, and a language preference or a remembered session is not
/// worth failing a page render over. Implementations swallow those errors and behave as though
/// nothing was stored.
/// </para>
/// <para>
/// <strong>Writes do not throw either, but they report whether the value stuck.</strong> Swallowing
/// silently is right for a preference and wrong for data the user cannot get back: Secret-Santa is
/// local-first with no backend (NFR-001), so the store holds the only copy of a group. Callers that
/// can afford the loss ignore the result; callers that cannot check it and surface the failure.
/// </para>
/// </remarks>
public interface IKeyValueStore
{
    /// <summary>Reads a value, or <see langword="null"/> if absent or unreadable.</summary>
    /// <param name="scope">The storage lifetime to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored value, or <see langword="null"/>.</returns>
    ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a value.</summary>
    /// <param name="scope">The storage lifetime to write to.</param>
    /// <param name="key">The key to write.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>
    /// <see langword="true"/> if the value was stored; <see langword="false"/> if the storage
    /// refused it (unavailable, full, or blocked). Never throws for those cases.
    /// </returns>
    ValueTask<bool> SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a value. Removing an absent key is not an error.</summary>
    /// <param name="scope">The storage lifetime to remove from.</param>
    /// <param name="key">The key to remove.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>A task that completes once the value is gone.</returns>
    ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default);
}
