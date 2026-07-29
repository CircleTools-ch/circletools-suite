using System.Reflection;
using AppSuite.Circles.Contracts;

namespace AppSuite.Architecture.Test;

/// <summary>
/// NFR-006's "no email addresses" rule, as an assembly scan rather than a per-endpoint assertion —
/// so an endpoint added later cannot quietly start returning one.
/// </summary>
public sealed class CircleContractsTests
{
    /// <summary>
    /// The only Circles contract types that may carry an address, and why.
    /// </summary>
    /// <remarks>
    /// <see cref="InviteRequest"/> is an <em>input</em>: inviting by email is the feature, and the
    /// address never comes back out of it. <see cref="PendingInvitationResponse"/> returns an address
    /// an owner or admin typed into this circle, which is what makes the pending list usable at all —
    /// and it says nothing about whether that address has an account, which is the property NFR-005
    /// protects. Neither is the case NFR-006 forbids: no <em>member's</em> address is ever returned.
    /// <para>
    /// Adding a name to this list is a decision, and it is meant to be an uncomfortable one.
    /// </para>
    /// </remarks>
    private static readonly string[] _mayCarryAnAddress =
    [
        nameof(InviteRequest),
        nameof(PendingInvitationResponse),
    ];

    [Fact]
    public void NoCircleDto_CarriesAnEmailField()
    {
        var offenders = ContractTypes()
            .Where(type => !_mayCarryAnAddress.Contains(type.Name, StringComparer.Ordinal))
            .SelectMany(type => type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(LooksLikeAnAddress)
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Keeps the exemption list honest: a name that no longer matches a type is an exemption that
    /// has silently stopped meaning anything.
    /// </summary>
    [Fact]
    public void EveryExemptedContractTypeStillExists()
    {
        var names = ContractTypes().Select(type => type.Name).ToList();

        Assert.All(_mayCarryAnAddress, exempted => Assert.Contains(exempted, names, StringComparer.Ordinal));
    }

    private static bool LooksLikeAnAddress(PropertyInfo property) =>
        property.PropertyType == typeof(string)
        && (property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Mail", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Address", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Type> ContractTypes() =>
        typeof(CreateCircleRequest).Assembly.GetExportedTypes().Where(type => type.IsClass);
}
