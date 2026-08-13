using CircleTools.Identity.Passwords;
using Microsoft.AspNetCore.Identity;

namespace CircleTools.Identity.Test;

/// <summary>
/// Covers the FR-007 breached-password rule and the bundled corpus behind it (ADR-0027).
/// </summary>
/// <remarks>
/// The corpus is an embedded resource, so these tests are also the guard against it going missing
/// or being rebuilt wrongly: a list that fails to load would make the check pass every password
/// silently, which is the one failure mode nothing else would notice.
/// </remarks>
public sealed class BreachedPasswordValidatorTests
{
    private static readonly EmbeddedBreachedPasswordList _list = new();

    private static BreachedPasswordValidator Validator() => new(_list);

    private static CircleToolsUser User() => new() { DisplayName = "Anna" };

    [Theory]
    [InlineData("passwordpassword")]
    [InlineData("qwertyuiopasdfgh")]
    [InlineData("iloveyouiloveyou")]
    [InlineData("123456789012")]
    public async Task BreachedPassword_IsRejectedWithItsOwnCode(string password)
    {
        var result = await Validator().ValidateAsync(null!, User(), password);

        Assert.False(result.Succeeded);
        Assert.Equal(
            BreachedPasswordValidator.BreachedPasswordCode,
            Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("correcthorsebatterystaple-circletools")]
    [InlineData("Znüni am Zürichsee im Juli")]
    public async Task APassphraseThatWasNeverBreached_IsAccepted(string password)
    {
        var result = await Validator().ValidateAsync(null!, User(), password);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Comparison_IsCaseSensitive()
    {
        // ADR-0027: the corpus holds literal breached passwords. "PasswordPassword" is not in it,
        // and rejecting it would mean refusing a password that was never actually exposed. This
        // documents the trade-off rather than asserting it is ideal.
        Assert.True(_list.Contains("passwordpassword"));

        var result = await Validator().ValidateAsync(null!, User(), "PasswordPassword");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AMissingPassword_IsNotThisValidatorsBusiness(string? password)
    {
        // Identity's own validators report a missing password; reporting it twice would show the
        // user two messages for one mistake.
        var result = await Validator().ValidateAsync(null!, User(), password);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void TheBundledCorpus_LoadsAndIsNotEmpty()
    {
        // A resource that failed to load would make every password pass. EmbeddedBreachedPasswordList
        // throws rather than return an empty set, but this pins the expectation at a useful size.
        Assert.True(
            _list.Count > 30_000,
            $"The bundled corpus holds {_list.Count} entries — expected the full ADR-0027 list.");
    }

    [Fact]
    public void TheBundledCorpus_HoldsNothingTheLengthRuleAlreadyRejects()
    {
        // ADR-0027's whole economy: entries under 12 characters can never be reached, because
        // PasswordOptions.RequiredLength rejects them first. Any that crept in would be dead
        // weight and a sign the list was rebuilt without the filter — so this reads the shipped
        // resource itself rather than spot-checking a few passwords.
        using var stream = typeof(EmbeddedBreachedPasswordList).Assembly
            .GetManifestResourceStream("CircleTools.Identity.Passwords.BreachedPasswords.txt");

        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!);

        var lines = 0;

        while (reader.ReadLine() is { } line)
        {
            Assert.True(line.Length >= 12, $"Line {lines + 1} is shorter than 12 characters: '{line}'");
            Assert.Equal(line.Trim(), line);
            lines++;
        }

        Assert.Equal(_list.Count, lines);
    }
}
