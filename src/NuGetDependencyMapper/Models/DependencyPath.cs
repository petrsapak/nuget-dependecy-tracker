namespace NuGetDependencyMapper.Models;

/// <summary>
/// Represents a single dependency chain from a direct dependency
/// down to a target package.
/// </summary>
public class DependencyPath
{
    public required IReadOnlyList<string> PackageNames { get; init; }
    public bool IsDirect => PackageNames.Count == 1;
}
