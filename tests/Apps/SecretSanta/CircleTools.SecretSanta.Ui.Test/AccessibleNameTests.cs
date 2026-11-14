// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Every tile-style element made clickable with <c>role="button"</c> carries an accessible name of its own
/// (Q18, 2026-10-16 device test: the group- and event-hub tiles were announced as unnamed buttons).
/// </summary>
public sealed class AccessibleNameTests
{
    private const string RoleButton = "role=\"button\"";

    [Fact]
    public void EveryRoleButtonElement_HasAnAriaLabel()
    {
        var ui = Path.Combine(RepoRoot(), "src", "Apps", "SecretSanta", "CircleTools.SecretSanta.Ui");
        var unnamed = new List<string>();

        foreach (var file in Directory.EnumerateFiles(ui, "*.razor", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            for (var at = source.IndexOf(RoleButton, StringComparison.Ordinal); at >= 0;
                 at = source.IndexOf(RoleButton, at + 1, StringComparison.Ordinal))
            {
                // The element's opening tag: from its '<' to the next '<' (its first child). Attribute values
                // here (lambdas, interpolations) hold quotes but no '<', so this bounds the tag well enough.
                var start = source.LastIndexOf('<', at);
                var end = source.IndexOf('<', at);
                var tag = source[start..(end < 0 ? source.Length : end)];
                if (!tag.Contains("aria-label=", StringComparison.Ordinal))
                {
                    var line = source[..at].Count(c => c == '\n') + 1;
                    unnamed.Add($"{Path.GetFileName(file)}:{line}");
                }
            }
        }

        Assert.True(unnamed.Count == 0, "role=\"button\" without aria-label: " + string.Join(", ", unnamed));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CircleTools.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (CircleTools.slnx) not found.");
    }
}
