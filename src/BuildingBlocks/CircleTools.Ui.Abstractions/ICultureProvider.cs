// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Owns the UI language: what it currently is, how it is changed, and how a change is announced.
/// </summary>
/// <remarks>
/// <para>
/// The provider persists the choice so it survives a reload, and raises <see cref="CultureChanged"/>
/// so the root of the component tree can re-render. <c>IStringLocalizer</c> resolves
/// <see cref="CultureInfo.CurrentUICulture"/> at lookup time, so re-rendering is enough to switch
/// language — a full page reload is not required, which is what
/// <c>UC-MAIN-005</c> means by "without a reload or a flash of the wrong language".
/// </para>
/// </remarks>
public interface ICultureProvider
{
    /// <summary>The culture currently in effect.</summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// The culture used for date/number/currency formatting — the device/OS region, kept independent of
    /// the UI language (ADR-0056). Exposed so a render boundary can re-assert both thread cultures before
    /// it renders, which is required on MAUI Blazor Hybrid where render batches run on a thread pool.
    /// </summary>
    CultureInfo FormattingCulture { get; }

    /// <summary>Raised after <see cref="SetCultureAsync"/> has applied and persisted a new culture.</summary>
    event EventHandler? CultureChanged;

    /// <summary>
    /// Loads the persisted culture and applies it to the current thread's defaults.
    /// </summary>
    /// <remarks>
    /// Called once during host startup, before the first render: components read the
    /// <see cref="CultureInfo"/> defaults as they render, so a later call would leave already-rendered
    /// output in the previous language.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The culture that was applied.</returns>
    ValueTask<CultureInfo> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies, persists and announces a new culture. Unknown or unsupported names fall back to the
    /// default culture rather than throwing.
    /// </summary>
    /// <param name="name">The culture name, for example <c>fr-CH</c>.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>A task that completes once the change has been applied and announced.</returns>
    ValueTask SetCultureAsync(string name, CancellationToken cancellationToken = default);
}
