using System.Collections.Frozen;
using System.Reflection;

namespace AppSuite.Identity.Passwords;

/// <summary>
/// The bundled breached-password corpus, read once from an embedded resource (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// The list holds only passwords of <strong>12 characters or more</strong>, because
/// <see cref="Microsoft.AspNetCore.Identity.PasswordOptions.RequiredLength"/> is 12 and anything
/// shorter is rejected before this check runs (FR-007). That filter is what keeps a useful slice of a
/// million-entry corpus down to a few hundred kilobytes.
/// </para>
/// <para>
/// Lookups are <strong>ordinal and case-sensitive</strong>: the corpus contains literal breached
/// passwords, and folding case would reject passphrases that were never breached.
/// </para>
/// <para>
/// Register this as a <em>singleton</em>. The set is built lazily on first use and costs a few
/// megabytes; building it per request would put file parsing on the sign-up path, which is exactly
/// the kind of asymmetric work NFR-005 forbids.
/// </para>
/// </remarks>
public sealed class EmbeddedBreachedPasswordList : IBreachedPasswordList
{
    private const string ResourceName = "AppSuite.Identity.Passwords.BreachedPasswords.txt";

    private readonly Lazy<FrozenSet<string>> _passwords = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The number of passwords in the bundled corpus.</summary>
    public int Count => _passwords.Value.Count;

    /// <inheritdoc />
    public bool Contains(string password) =>
        !string.IsNullOrEmpty(password) && _passwords.Value.Contains(password);

    private static FrozenSet<string> Load()
    {
        using var stream = typeof(EmbeddedBreachedPasswordList).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The breached-password list '{ResourceName}' is missing from the assembly. " +
                "It is an EmbeddedResource in AppSuite.Identity.csproj; without it the FR-007 " +
                "breached-password check would silently pass every password.");

        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                passwords.Add(line);
            }
        }

        return passwords.ToFrozenSet(StringComparer.Ordinal);
    }
}
