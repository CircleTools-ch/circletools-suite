namespace AppSuite.Main.Ui.Api;

/// <summary>
/// The two <see cref="HttpClient"/> registrations the Main app uses, told apart by a DI key.
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
/// </remarks>
public static class MainApiClients
{
    /// <summary>Service key for the client that attaches the bearer token and renews it.</summary>
    public const string Authorized = "AppSuite.Main.Api";

    /// <summary>Service key for the plain client used by sign-in, sign-up and refresh.</summary>
    public const string Anonymous = "AppSuite.Main.Api.Anonymous";

    /// <summary>The version-prefixed route base every account endpoint sits under (ADR-0010).</summary>
    public const string AccountRoute = "api/v1/account";

    /// <summary>The route base for circles (FEAT-003).</summary>
    public const string CirclesRoute = "api/v1/circles";

    /// <summary>
    /// The route base for invitations (FEAT-003). Separate from <see cref="CirclesRoute"/> because
    /// its routes exist for people who are <em>not</em> in the circle yet, and the role filter on the
    /// circles group answers <c>404</c> to exactly them.
    /// </summary>
    public const string InvitationsRoute = "api/v1/invitations";
}
