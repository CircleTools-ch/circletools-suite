// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Circles.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Circles.Repository.Test;

/// <summary>
/// NFR-004's data-minimisation clause as a fitness function over the <c>circles</c> persisted model.
/// </summary>
/// <remarks>
/// <para>
/// The Main model has carried this check since M1. It matters more here: a circle describes people
/// who <em>did not enter their own data</em>. A managed member is a profile someone else created,
/// and the pressure to "just add a phone number so we can reach Grosi" will arrive from a real
/// product need rather than from carelessness. This is the assertion that makes adding one a
/// deliberate act.
/// </para>
/// <para>
/// It needs no database: the model is built from the mapping, not from a connection.
/// </para>
/// </remarks>
public sealed class PersistedModelReviewTests
{
    /// <summary>Fragments that may not appear in any persisted property name (NFR-004).</summary>
    /// <remarks>
    /// Matched as case-insensitive substrings, so <c>BillingPostalCode</c> is caught as surely as
    /// <c>PostalCode</c>. Kept identical to the Main model's list — one rule, checked in both
    /// schemas, rather than two lists drifting apart.
    /// </remarks>
    private static readonly string[] _forbidden =
    [
        "birth",      // date of birth
        "postal",
        "zipcode",
        "postcode",
        "street",
        "phone",
        "mobile",
        "payment",
        "creditcard",
        "cardnumber",
        "iban",
        "vatnumber",
    ];

    /// <summary>
    /// Property names that contain a forbidden fragment but are not personal data.
    /// </summary>
    /// <remarks>
    /// Empty by default, and an entry here is a decision to allow something the requirement forbids.
    /// It should be uncomfortable to add.
    /// </remarks>
    private static readonly string[] _allowed = [];

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddCirclesRepository("Host=localhost;Database=modeltests;Username=u;Password=p");

        return services.BuildServiceProvider();
    }

    [Fact]
    public void NoPersistedProperty_HoldsDataTheSuitePromisedNotToKeep()
    {
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<CirclesDbContext>();

        var offenders = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Select(property => new { Entity = entity.ShortName(), Property = property.Name }))
            .Where(p => !_allowed.Contains(p.Property, StringComparer.OrdinalIgnoreCase))
            .Where(p => _forbidden.Any(f => p.Property.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .Select(p => $"{p.Entity}.{p.Property}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "NFR-004 forbids storing a date of birth, postal address, phone number or payment data. "
            + $"The circles model persists: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void AManagedMember_PersistsNothingBeyondANameAndALanguage()
    {
        // FR-019 is explicit that a managed member has no address. Pinned as the exact column list
        // rather than as an absence, because the way this rule breaks is by something being added
        // beside the name, not by DisplayName disappearing.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<CirclesDbContext>();

        var membership = context.Model.FindEntityType(typeof(CircleMembership))!;
        var properties = membership.GetProperties()
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(["CircleId", "DisplayName", "Id", "JoinedAt", "Language", "Role", "UserId"], properties);
    }

    [Fact]
    public void AnInvitation_StoresTheHandleOfItsSecretAndNeverTheSecret()
    {
        // The same rule as refresh tokens: a dump of this table yields no usable links. There is no
        // grace-window exception here — unlike a refresh token, an invitation has no racing
        // successor to tolerate.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<CirclesDbContext>();

        var invitation = context.Model.FindEntityType(typeof(Invitation))!;
        var secrets = invitation.GetProperties()
            .Select(p => p.Name)
            .Where(name => name.Contains("secret", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(["SecretHandle"], secrets);
    }

    [Fact]
    public void TheCounters_HoldAHashOfTheRecipient_NotTheAddress()
    {
        // The rate-limit rows are the one place an address that may belong to a stranger is counted
        // rather than sent to. They keep the hash, and they are swept once no limit counts them.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<CirclesDbContext>();

        var attempt = context.Model.FindEntityType(typeof(InvitationAttempt))!;
        var properties = attempt.GetProperties()
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(["AttemptedAt", "Id", "InviterUserId", "RecipientEmailHash"], properties);
    }

    [Fact]
    public void EveryTable_LivesInTheCirclesSchema()
    {
        // The seam ADR-0003 asks for: one database, but a schema boundary that makes splitting the
        // context out later a deployment change rather than a rewrite.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<CirclesDbContext>();

        var strays = context.Model.GetEntityTypes()
            .Where(entity => entity.GetSchema() is not CirclesDbContext.Schema)
            .Select(entity => $"{entity.ShortName()} → {entity.GetSchema() ?? "(default)"}")
            .ToList();

        Assert.Empty(strays);
    }
}
