// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Identity.Tokens;

namespace CircleTools.Identity.Test;

/// <summary>
/// Covers PKCE verification for the SSO hand-off (FR-031, ADR-0039). Possession of the code is not
/// enough — the redeemer must prove the verifier the challenge was derived from.
/// </summary>
public sealed class PkceTests
{
    private const string Verifier = "a-high-entropy-verifier-kept-in-the-clients-memory";

    [Fact]
    public void Verify_AcceptsTheMatchingVerifier()
    {
        var challenge = Pkce.ChallengeFor(Verifier);

        Assert.True(Pkce.Verify(Verifier, challenge, Pkce.MethodS256));
    }

    [Fact]
    public void Verify_RejectsAWrongVerifier()
    {
        var challenge = Pkce.ChallengeFor(Verifier);

        Assert.False(Pkce.Verify("not-the-verifier", challenge, Pkce.MethodS256));
    }

    [Fact]
    public void Verify_RejectsANonS256Method()
    {
        // 'plain' would let a skimmed challenge be replayed as the verifier — the whole point of S256
        // is to forbid it.
        Assert.False(Pkce.Verify(Verifier, Verifier, "plain"));
    }

    [Theory]
    [InlineData(null, "challenge")]
    [InlineData("", "challenge")]
    [InlineData("verifier", null)]
    [InlineData("verifier", "")]
    public void Verify_RejectsNullOrEmptyInputs(string? verifier, string? challenge)
    {
        Assert.False(Pkce.Verify(verifier, challenge, Pkce.MethodS256));
    }
}
