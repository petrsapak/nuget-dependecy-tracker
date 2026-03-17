using NuGetDependencyMapper.Models;

namespace NuGetDependencyMapper.Services;

public interface IDependencyGraphBuilder
{
    /// <summary>
    /// Builds a dependency graph from the project.assets.json content,
    /// selecting the appropriate target framework.
    /// </summary>
    DependencyGraph Build(string projectName, string assetsJsonContent, string? requestedFramework);
}
