using Microsoft.AspNetCore.Identity;

namespace AppSuite.Identity.Passwords;

/// <summary>
/// Rejects passwords that appear in a public breach corpus (FR-007, ADR-0027).
/// </summary>
/// <remarks>
/// This is a separate <see cref="IPasswordValidator{TUser}"/> rather than an
/// <see cref="IdentityOptions"/> setting because Identity has no such setting: its password options
/// cover length and composition only. Registering it as a validator means every path that sets a
/// password — sign-up (FR-007), password reset (FR-009) and any future change-password flow — is
/// covered without each one remembering to ask.
/// </remarks>
/// <param name="breached">The corpus to check against.</param>
public sealed class BreachedPasswordValidator(IBreachedPasswordList breached)
    : IPasswordValidator<AppSuiteUser>
{
    /// <summary>
    /// The error code reported when a password is found in the corpus.
    /// </summary>
    /// <remarks>
    /// Callers surface the <em>code</em>, not the description: the description is English and the
    /// product is not (NFR-003). See <c>ApiErrorText</c> on the client for the mapping.
    /// </remarks>
    public const string BreachedPasswordCode = "BreachedPassword";

    private readonly IBreachedPasswordList _breached = breached
        ?? throw new ArgumentNullException(nameof(breached));

    /// <inheritdoc />
    public Task<IdentityResult> ValidateAsync(
        UserManager<AppSuiteUser> manager,
        AppSuiteUser user,
        string? password)
    {
        // A null password is not this validator's business - Identity's own validators report it.
        if (string.IsNullOrEmpty(password) || !_breached.Contains(password))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = BreachedPasswordCode,
            Description =
                "This password appears in a public list of passwords exposed in data breaches. " +
                "Please choose a different one.",
        }));
    }
}
