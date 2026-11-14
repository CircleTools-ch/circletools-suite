// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Bunit;
using CircleTools.Main.Ui.Test.TestSupport;
using CircleTools.Ui.Account.Components;

namespace CircleTools.Main.Ui.Test;

/// <summary>The show/hide toggle of the account pages' password fields (PR-13).</summary>
public sealed class PasswordFieldTests
{
    [Fact]
    public async Task IsMaskedAtFirst_AndOffersToShow()
    {
        await using var ctx = new AccountTestContext();

        var cut = ctx.Render<PasswordField>(parameters => parameters.AddUnmatched("id", "field"));

        Assert.Equal("password", cut.Find("#field").GetAttribute("type"));
        Assert.NotNull(cut.Find("button[aria-label=ShowPassword]"));
    }

    [Fact]
    public async Task TheToggle_ShowsAndHidesTheText_AndKeepsIt()
    {
        await using var ctx = new AccountTestContext();

        var cut = ctx.Render<PasswordField>(parameters => parameters
            .Add(field => field.Value, "typed")
            .AddUnmatched("id", "field"));

        cut.Find("button[aria-label=ShowPassword]").Click();
        Assert.Equal("text", cut.Find("#field").GetAttribute("type"));
        Assert.Equal("typed", cut.Find("#field").GetAttribute("value"));

        cut.Find("button[aria-label=HidePassword]").Click();
        Assert.Equal("password", cut.Find("#field").GetAttribute("type"));
        Assert.Equal("typed", cut.Find("#field").GetAttribute("value"));
    }

    [Fact]
    public async Task TheToggle_DoesNotSubmitTheForm()
    {
        await using var ctx = new AccountTestContext();

        var cut = ctx.Render<PasswordField>();

        Assert.Equal("button", cut.Find("button[aria-label=ShowPassword]").GetAttribute("type"));
    }

    /// <summary>Revealed, the input is an ordinary text input, which keyboards would otherwise correct.</summary>
    [Fact]
    public async Task TheKeyboardIsToldNotToCorrectThePassword()
    {
        await using var ctx = new AccountTestContext();

        var cut = ctx.Render<PasswordField>(parameters => parameters
            .AddUnmatched("id", "field")
            .AddUnmatched("autocomplete", "new-password"));

        var input = cut.Find("#field");
        Assert.Equal("new-password", input.GetAttribute("autocomplete"));
        Assert.Equal("none", input.GetAttribute("autocapitalize"));
        Assert.Equal("off", input.GetAttribute("autocorrect"));
        Assert.Equal("false", input.GetAttribute("spellcheck"));
    }
}
