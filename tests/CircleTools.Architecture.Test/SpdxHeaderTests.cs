// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Architecture.Test;

/// <summary>
/// Every hand-written source file carries the SPDX license header (ADR-0099).
/// </summary>
/// <remarks>
/// C# files are also held to this by IDE0073 at build time (<c>.editorconfig</c>), which is what makes an
/// IDE insert the header into a new file. This test covers the file types Roslyn cannot see — Razor,
/// JavaScript, CSS, XAML, HTML, shell scripts, SQL, Dockerfiles and the Caddyfile — so a new component or
/// script without a header fails CI too. Generated code (EF Core migrations, <c>*.Designer.cs</c>) and
/// build output are exempt.
/// </remarks>
public sealed class SpdxHeaderTests
{
    private const string LicenseTag = "SPDX-License-Identifier: Apache-2.0";

    /// <summary>How many leading lines may come before the header (a shebang, XML declaration or doctype).</summary>
    private const int HeaderWindow = 4;

    private static readonly string[] _roots = ["src", "tests", "deploy"];

    private static readonly string[] _extensions =
        [".cs", ".razor", ".js", ".css", ".xaml", ".html", ".sh", ".ps1", ".sql"];

    private static readonly string[] _fileNames = ["Caddyfile"];

    private static readonly string[] _excludedDirectories =
        ["bin", "obj", "Migrations", "node_modules", ".playwright", "artifacts"];

    [Fact]
    public void EverySourceFile_StartsWithTheSpdxLicenseHeader()
    {
        var repositoryRoot = RepositoryRoot();

        var missing = _roots
            .Select(root => Path.Combine(repositoryRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(IsCovered)
            .Where(path => !HasHeader(path))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These files have no '{LicenseTag}' header (ADR-0099):{Environment.NewLine}"
            + string.Join(Environment.NewLine, missing));
    }

    private static bool IsCovered(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (segments.Any(segment => _excludedDirectories.Contains(segment, StringComparer.Ordinal)))
        {
            return false;
        }

        var name = Path.GetFileName(path);

        if (name.EndsWith(".Designer.cs", StringComparison.Ordinal)
            || name.EndsWith(".g.cs", StringComparison.Ordinal)
            || name.EndsWith(".g.i.cs", StringComparison.Ordinal))
        {
            return false;
        }

        return _fileNames.Contains(name, StringComparer.Ordinal)
            || name.EndsWith("Dockerfile", StringComparison.Ordinal)
            || _extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasHeader(string path) =>
        File.ReadLines(path).Take(HeaderWindow).Any(line => line.Contains(LicenseTag, StringComparison.Ordinal));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CircleTools.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory!.FullName;
    }
}
