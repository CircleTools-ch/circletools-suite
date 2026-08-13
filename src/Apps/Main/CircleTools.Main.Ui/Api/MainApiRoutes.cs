namespace CircleTools.Main.Ui.Api;

/// <summary>
/// The Main-app-only API route bases (FEAT-003). The account routes and the shared client keys moved
/// to <c>CircleTools.Ui.Account.Api.AccountApiClients</c> with the account UI (ADR-0034); circles and
/// invitations are Main-only (ADR-0033), so their routes stay here.
/// </summary>
public static class MainApiRoutes
{
    /// <summary>The route base for circles (FEAT-003).</summary>
    public const string CirclesRoute = "api/v1/circles";

    /// <summary>
    /// The route base for invitations (FEAT-003). Separate from <see cref="CirclesRoute"/> because
    /// its routes exist for people who are <em>not</em> in the circle yet, and the role filter on the
    /// circles group answers <c>404</c> to exactly them.
    /// </summary>
    public const string InvitationsRoute = "api/v1/invitations";
}
