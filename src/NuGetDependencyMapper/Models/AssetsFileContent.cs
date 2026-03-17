namespace NuGetDependencyMapper.Models;

/// <summary>
/// Raw parsed content from a project.assets.json file,
/// before framework selection or graph construction.
/// </summary>
public class AssetsFileContent
{
    /// <summary>
    /// All target framework monikers found in the project section.
    /// </summary>
    public required IReadOnlyList<string> AvailableFrameworks { get; init; }

    /// <summary>
    /// Direct package dependencies per framework.
    /// Key: framework moniker, Value: list of direct package names.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> DirectDependenciesByFramework { get; init; }

    /// <summary>
    /// All resolved packages per target key (may differ from framework key).
    /// Key: target key (e.g., ".NETFramework,Version=v4.8"), Value: package lookup.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, PackageInfo>> PackagesByTarget { get; init; }

    /// <summary>
    /// All target keys found in the targets section.
    /// </summary>
    public required IReadOnlyList<string> AvailableTargets { get; init; }
}
