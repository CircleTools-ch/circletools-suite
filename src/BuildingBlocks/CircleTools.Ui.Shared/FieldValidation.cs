// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using MudBlazor.Interfaces;

namespace CircleTools.Ui.Shared;

/// <summary>
/// Resets the fields a form is about to empty after a successful submit.
/// </summary>
/// <remarks>
/// A MudBlazor field the user typed into is "touched", and a touched <c>Required</c> field validates again when
/// its value changes — so emptying it from code after a successful save showed "Required" under every field,
/// right below the success message (BTP-3, 2026-10-30). Resetting the fields puts them back to their
/// fresh state: empty, no error, until the user touches them again.
/// </remarks>
public static class FieldValidation
{
    /// <summary>
    /// Resets each field to its fresh state: empty, no error, untouched. Fields not rendered
    /// (<see langword="null"/>) are skipped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call it <em>before</em> setting the bound values to empty; the field reports <see langword="null"/>
    /// through its <c>ValueChanged</c>, which the caller's empty string then replaces.
    /// </para>
    /// <para>
    /// It must be the field's full reset. MudBlazor's <c>ResetValidationAsync</c> clears the errors but not
    /// <c>Touched</c> (9.11.0), so a touched field still validates the emptied value when it arrives and
    /// shows "Required" again. Clearing first and resetting the validation after only worked when that
    /// validation happened to finish before the reset; on a busy machine it did not (CI on <c>main</c>,
    /// 2026-11-24; 3 of 12 runs under CPU load).
    /// </para>
    /// </remarks>
    /// <param name="fields">The fields to reset.</param>
    /// <returns>A task that completes when every field is reset.</returns>
    public static async Task ResetAsync(params IFormComponent?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        foreach (var field in fields)
        {
            if (field is not null)
            {
                await field.ResetAsync();
            }
        }
    }
}
