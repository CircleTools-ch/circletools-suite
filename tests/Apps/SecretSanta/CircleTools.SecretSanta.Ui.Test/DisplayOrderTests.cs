// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// People are listed alphabetically in the reader's language (backlog LO-1, 2026-11-01), not in the order they
/// were added.
/// </summary>
public sealed class DisplayOrderTests
{
    [Theory]
    [InlineData("de-CH")]
    [InlineData("en")]
    [InlineData("fr-CH")]
    public void SortsAlphabetically_IgnoringCase(string culture) =>
        InCulture(culture, () =>
            Assert.Equal(["anna", "Ben", "chris", "Dora"], DisplayOrder.ByName(["Dora", "chris", "anna", "Ben"], n => n)));

    [Fact]
    public void AnUmlautSortsWithItsBaseLetter_NotAfterZ() =>
        InCulture("de-CH", () =>
            Assert.Equal(["Anna", "Ännchen", "Bruno", "Zoe"], DisplayOrder.ByName(["Zoe", "Ännchen", "Bruno", "Anna"], n => n)));

    [Fact]
    public void AnAccentedNameSortsWithItsBaseLetter() =>
        InCulture("fr-CH", () =>
            Assert.Equal(["Eliane", "Élodie", "Emma", "Fabien"], DisplayOrder.ByName(["Fabien", "Emma", "Élodie", "Eliane"], n => n)));

    [Fact]
    public void EqualNamesKeepTheirStoredOrder()
    {
        var first = (Id: 1, Name: "Anna");
        var second = (Id: 2, Name: "anna");

        var ordered = InCulture("en", () => DisplayOrder.ByName([(Id: 3, Name: "Ben"), first, second], p => p.Name));

        Assert.Equal([1, 2, 3], ordered.Select(p => p.Id));
    }

    private static void InCulture(string culture, Action act) => InCulture(culture, () =>
    {
        act();
        return 0;
    });

    private static T InCulture<T>(string culture, Func<T> act)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            return act();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
        }
    }
}
