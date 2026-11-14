// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Turns NFR-004's data-minimisation clause into a fitness function over the whole persisted model.
/// </summary>
/// <remarks>
/// <para>
/// NFR-004 lists this criterion under <c>verification: review</c> — "given the persisted data model,
/// when it is reviewed against this requirement". A review happens once and then rots; this asserts
/// it on every build instead.
/// </para>
/// <para>
/// The rule caught a real breach when it was written: <c>IdentityUser</c> ships
/// <c>PhoneNumber</c> and <c>PhoneNumberConfirmed</c>, and the first Identity migration created both
/// columns although nothing in the suite ever reads them. They are now mapped away in
/// <see cref="MainDbContext"/>. A framework base class is exactly how a forbidden column arrives
/// without anyone deciding to add one, which is why this checks the <em>model</em> rather than the
/// classes the team wrote.
/// </para>
/// </remarks>
public sealed class PersistedModelReviewTests
{
    /// <summary>
    /// Fragments that may not appear in any persisted property name (NFR-004).
    /// </summary>
    /// <remarks>
    /// Deliberately matched as substrings, case-insensitively: the point is to catch
    /// <c>BillingPostalCode</c> or <c>UserPhoneNumber</c>, not just an exact <c>PhoneNumber</c>.
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
    /// Kept explicit and empty-by-default: an entry here is a decision to allow something the
    /// requirement forbids, and it should be uncomfortable to add.
    /// </remarks>
    private static readonly string[] _allowed = [];

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MainDbContext>(o =>
            o.UseNpgsql("Host=localhost;Database=modeltests;Username=u;Password=p"));
        services.AddCircleToolsIdentityCore();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void NoPersistedProperty_HoldsDataTheSuitePromisedNotToKeep()
    {
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

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
            + $"The model persists: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void TheUser_PersistsNoPhoneNumber()
    {
        // Pinned separately because this one was actually present, and a regression would most
        // likely arrive by someone removing the two Ignore calls rather than adding a property.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var user = context.Model.FindEntityType(typeof(CircleToolsUser))!;

        Assert.Null(user.FindProperty(nameof(CircleToolsUser.PhoneNumber)));
        Assert.Null(user.FindProperty(nameof(CircleToolsUser.PhoneNumberConfirmed)));
    }

    [Fact]
    public void ThePassword_IsPersistedOnlyAsAHash()
    {
        // NFR-004 credential storage. Identity stores PasswordHash and nothing else; this fails
        // loudly if a plaintext or reversible column is ever introduced beside it.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var user = context.Model.FindEntityType(typeof(CircleToolsUser))!;
        var passwordish = user.GetProperties()
            .Select(p => p.Name)
            .Where(name => name.Contains("password", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal([nameof(CircleToolsUser.PasswordHash)], passwordish);
    }

    [Fact]
    public void TheRefreshTokenTable_StoresHandlesNotSecrets()
    {
        // ADR-0021/ADR-0026: the client's refresh token is hashed into a handle before storage, so
        // a database dump cannot be replayed as a session. The one exception is SuccessorSecret,
        // held only for the racing-refresh grace window.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var token = context.Model.FindEntityType(typeof(CircleTools.Identity.Tokens.RefreshToken))!;
        var secrets = token.GetProperties()
            .Select(p => p.Name)
            .Where(name => name.Contains("secret", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(["SuccessorSecret"], secrets);
    }
}
