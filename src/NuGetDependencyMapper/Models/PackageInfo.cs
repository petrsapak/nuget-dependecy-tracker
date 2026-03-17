namespace NuGetDependencyMapper.Models;

/// <summary>
/// Represents a resolved NuGet package with its version and immediate dependencies.
/// </summary>
public record PackageInfo(
    string Name,
    string Version,
    IReadOnlyDictionary<string, string> Dependencies);
