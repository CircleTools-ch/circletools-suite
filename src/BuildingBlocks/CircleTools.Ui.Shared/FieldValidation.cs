// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using MudBlazor.Interfaces;

namespace CircleTools.Ui.Shared;

/// <summary>
/// Clears the validation state of fields a form has just emptied after a successful submit.
/// </summary>
/// <remarks>
/// A MudBlazor field the user typed into is "touched", and a touched <c>Required</c> field validates again when
/// its value changes — so emptying it from code after a successful save showed "Required" under every field,
/// right below the success message (BTP-3, 2026-10-30). Resetting the validation puts the fields back to their
/// fresh state: empty, no error, until the user touches them again.
/// </remarks>
public static class FieldValidation
{
    /// <summary>Resets the validation of each field; fields not rendered (<see langword="null"/>) are skipped.</summary>
    /// <remarks>
    /// The field re-validates when it receives the emptied value, which happens on the page's next render — so
    /// the caller clears the bound values and calls <c>StateHasChanged()</c> first, and the reset runs after
    /// that render (the yield lets it happen).
    /// </remarks>
    /// <param name="fields">The fields to reset.</param>
    /// <returns>A task that completes when every field is reset.</returns>
    public static async Task ResetAsync(params IFormComponent?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        await Task.Yield();
        foreach (var field in fields)
        {
            if (field is not null)
            {
                await field.ResetValidationAsync();
            }
        }
    }
}
