namespace CircleTools.Ui.Account.Auth;

/// <summary>
/// Where the cross-app SSO authority lives, and how this app identifies itself (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// The authority is the origin that holds the session a hand-off draws from — the Main shell. Each
/// app supplies this from its own configuration (like <c>ApiBaseUrl</c>), so it is inert for an
/// account-free visitor who never starts a hand-off. An app that <em>is</em> the authority still
/// carries the value harmlessly; it simply never redirects to itself.
/// </remarks>
/// <param name="AuthorityBaseUrl">The authority origin, e.g. <c>https://circletools.ch</c>.</param>
/// <param name="ClientId">A stable identifier for this app, sent when a code is minted.</param>
public sealed record SsoConnectOptions(string AuthorityBaseUrl, string ClientId);
