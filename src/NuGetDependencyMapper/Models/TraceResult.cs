namespace NuGetDependencyMapper.Models;

/// <summary>
/// Contains all dependency paths leading to a specific target package.
/// </summary>
public class TraceResult
{
    public required string TargetPackage { get; init; }
    public required string TargetVersion { get; init; }
    public required IReadOnlyList<DependencyPath> Paths { get; init; }
}
