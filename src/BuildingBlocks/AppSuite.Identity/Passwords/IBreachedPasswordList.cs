namespace AppSuite.Identity.Passwords;

/// <summary>
/// A source of passwords known to appear in public breach corpora (FR-007, ADR-0027).
/// </summary>
/// <remarks>
/// This is the seam that keeps the choice of corpus an implementation detail. The shipped
/// implementation is an offline list bundled with the assembly; a deployment may later swap in the
/// Have I Been Pwned range API without any caller changing, which is the whole reason the check is
/// expressed as an interface rather than a static lookup.
/// </remarks>
public interface IBreachedPasswordList
{
    /// <summary>Reports whether a password is known to have been breached.</summary>
    /// <param name="password">The candidate password, in the clear.</param>
    /// <returns>
    /// <see langword="true"/> when the password appears in the corpus. A <see langword="false"/>
    /// means only "not in this corpus" — never "this password has not been breached".
    /// </returns>
    bool Contains(string password);
}
