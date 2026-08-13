using System.Text.RegularExpressions;
using CircleTools.Main.Ui.Api;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Pins the UI's refusal-code table to the codes the API can actually send (FEAT-003, NFR-003).
/// </summary>
/// <remarks>
/// <para>
/// The two live in different projects and are joined only by a string. A code added to
/// <c>CircleProblems</c> without a matching entry in <c>CircleErrorText</c> compiles, ships, and
/// shows the user a generic "there was a conflict" instead of the sentence that would tell them what
/// to do — a user-visible regression no other test would catch.
/// </para>
/// <para>
/// The API side is read out of its source rather than by invoking the mapper: the codes are baked
/// into <c>Results.Problem</c> extension dictionaries that would require an HTTP round trip to
/// observe, and this test is meant to fail at build time.
/// </para>
/// </remarks>
public sealed class CircleErrorCodeCoverageTests
{
    /// <summary>
    /// A refusal code as it appears in source: a snake_case string literal. Field names and
    /// validation codes ("name", "required") are single words and do not match.
    /// </summary>
    private static readonly Regex _snakeCaseLiteral = new(
        """"(?<code>[a-z][a-z0-9]*(?:_[a-z0-9]+)+)"""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// The code argument of a problem factory call, which catches the ones that happen to be a
    /// single word — <c>refused</c>, the fallback, is one and would otherwise slip through.
    /// </summary>
    private static readonly Regex _factoryCallCode = new(
        "(?:Problem|Conflict|TooManyRequests)\\(\\s*(?:StatusCodes\\.\\w+,\\s*)?\"(?<code>[a-z0-9_]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Every place a circle refusal code is minted. <c>CircleProblems</c> maps the domain's errors;
    /// the deletion guard mints its own, because it refuses before any circle handler is reached
    /// (FR-015).
    /// </summary>
    private static readonly string[][] _sources =
    [
        ["src", "Apps", "Main", "CircleTools.Main.Service", "CircleProblems.cs"],
        ["src", "Apps", "Main", "CircleTools.Main.Service", "SoleOwnerCircleGuard.cs"],
    ];

    [Fact]
    public void EveryRefusalCodeTheApiCanSend_HasATranslatedSentence()
    {
        var apiCodes = CodesInCircleProblems();
        var uiCodes = KnownUiCodes();

        Assert.NotEmpty(apiCodes);

        var missing = apiCodes.Except(uiCodes).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            $"CircleErrorText has no resource key for: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// The other direction. A stale entry is harmless at runtime but means the table no longer
    /// describes the API, and the next reader cannot tell which entries are real.
    /// </summary>
    [Fact]
    public void TheUiTable_DoesNotDescribeRefusalsTheApiCannotSend()
    {
        var apiCodes = CodesInCircleProblems();
        var uiCodes = KnownUiCodes();

        var stale = uiCodes.Except(apiCodes).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(
            stale.Count == 0,
            $"CircleErrorText maps codes CircleProblems never sends: {string.Join(", ", stale)}");
    }

    private static HashSet<string> CodesInCircleProblems() =>
    [
        .. _sources
            .Select(parts => File.ReadAllText(SourcePath(parts)))
            .SelectMany(source => _snakeCaseLiteral.Matches(source)
                .Concat(_factoryCallCode.Matches(source))
                .Select(m => m.Groups["code"].Value)),
    ];

    private static HashSet<string> KnownUiCodes() => [.. CircleErrorText.KnownCodes];

    private static string SourcePath(string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CircleTools.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine([directory!.FullName, .. parts]);
    }
}
