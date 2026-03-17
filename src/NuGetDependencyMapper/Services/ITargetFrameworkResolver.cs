namespace NuGetDependencyMapper.Services;

public interface ITargetFrameworkResolver
{
    /// <summary>
    /// Resolves a framework moniker to a matching targets key.
    /// Handles short aliases (net48), long-form (.NETFramework,Version=v4.8),
    /// and RID-suffixed keys (net8.0-windows).
    /// </summary>
    string? ResolveTargetsKey(string frameworkMoniker, IReadOnlyList<string> availableTargets);
}
