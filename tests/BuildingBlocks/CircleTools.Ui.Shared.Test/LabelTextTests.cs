// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using CircleTools.Ui.Shared;

namespace CircleTools.Ui.Shared.Test;

/// <summary>
/// French puts a no-break space before the colon; German and English put none (the « Pour: Anna » defect,
/// 2026-10-28).
/// </summary>
public sealed class LabelTextTests
{
    [Theory]
    [InlineData("fr-CH", "Pour :")]
    [InlineData("fr", "Pour :")]
    [InlineData("de-CH", "Pour:")]
    [InlineData("en", "Pour:")]
    public void WithColon_SpacesTheColonPerLanguage(string culture, string expected) =>
        Assert.Equal(expected, LabelText.WithColon("Pour", CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void WithColon_FollowsTheCurrentUiCulture()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CH");
#pragma warning disable CA1304 // The overload that reads the current UI culture is what is under test.
            Assert.Equal("Budget :", LabelText.WithColon("Budget"));
#pragma warning restore CA1304
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
