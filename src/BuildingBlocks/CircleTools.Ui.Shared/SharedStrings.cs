namespace CircleTools.Ui.Shared;

/// <summary>
/// The resource anchor for strings belonging to shared components, resolved as
/// <c>IStringLocalizer&lt;SharedStrings&gt;</c> against <c>Resources/SharedStrings.*.resx</c>.
/// </summary>
/// <remarks>
/// Deliberately tiny. Only strings a shared component renders on its own behalf belong here; a
/// component that shows caller-supplied text takes it as a parameter, so the wording stays with the
/// app that owns the screen.
/// </remarks>
public sealed class SharedStrings;
