namespace NuGetDependencyMapper.Models;

/// <summary>
/// Represents the complete dependency graph for a project, including
/// direct package references and all resolved transitive dependencies.
/// </summary>
public class DependencyGraph
{
    public required string ProjectName { get; init; }
    public required string TargetFramework { get; init; }
    public required IReadOnlyList<string> DirectDependencies { get; init; }
    public required IReadOnlyDictionary<string, PackageInfo> Packages { get; init; }
    public required IReadOnlyList<string> AvailableFrameworks { get; init; }
}
