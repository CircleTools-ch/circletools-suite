namespace CircleTools.Ui.Account;

/// <summary>
/// Marker type for the shared account UI string resources (ADR-0034). Used as the generic argument to
/// <c>IStringLocalizer&lt;AccountStrings&gt;</c>; the actual strings live in
/// <c>Resources/AccountStrings.*.resx</c> (ADR-0011). Every frontend that hosts the account pages
/// resolves them through this one marker, so sign-in, verification and the account screens read the
/// same in each app.
/// </summary>
public sealed class AccountStrings;
