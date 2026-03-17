using System.Text.RegularExpressions;

namespace NuGetDependencyMapper.Services;

public partial class SolutionParser : ISolutionParser
{
    // Matches: Project("{GUID}") = "Name", "path\to\project.csproj", "{GUID}"
    [GeneratedRegex(
        @"^Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+"",\s*""([^""]+\.csproj)""\s*,",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ProjectLinePattern();

    public IReadOnlyList<string> GetProjectPaths(string solutionFilePath, string solutionContent)
    {
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionFilePath))!;
        var matches = ProjectLinePattern().Matches(solutionContent);

        return matches
            .Select(m => m.Groups[1].Value)
            .Select(relativePath => Path.GetFullPath(
                Path.Combine(solutionDirectory, relativePath.Replace('\\', Path.DirectorySeparatorChar))))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
