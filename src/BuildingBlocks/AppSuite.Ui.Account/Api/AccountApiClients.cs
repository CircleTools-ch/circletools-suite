namespace AppSuite.Ui.Account.Api;

/// <summary>
/// The two <see cref="HttpClient"/> registrations the account API uses, told apart by a DI key
/// (ADR-0034; moved here from the Main app's <c>MainApiClients</c>).
/// </summary>
/// <remarks>
/// <para>
/// They differ in exactly one way: <see cref="Authorized"/> runs through
/// <c>AuthorizedHttpMessageHandler</c> and <see cref="Anonymous"/> does not. Both point at the same
/// API and both send cookies for the WebAuthn ceremonies.
/// </para>
/// <para>
/// The split is not cosmetic. Refreshing a session must not go through the handler that triggers
/// refreshes — that is a loop — and the composition root would otherwise have a genuine dependency
/// cycle: client → handler → refresher → client. Keying them apart is what breaks it.
/// </para>
/// <para>
/// The keys are shared by every client over this API (account here, and the Main app's circles
/// client), so they live in this block rather than in any one app.
/// </para>
/// </remarks>
public static class AccountApiClients
{
    /// <summary>Service key for the client that attaches the bearer token and renews it.</summary>
    public const string Authorized = "AppSuite.Account.Api";

    /// <summary>Service key for the plain client used by sign-in, sign-up and refresh.</summary>
    public const string Anonymous = "AppSuite.Account.Api.Anonymous";

    /// <summary>The version-prefixed route base every account endpoint sits under (ADR-0010).</summary>
    public const string AccountRoute = "api/v1/account";
}
