using NuGetDependencyMapper.Models;

namespace NuGetDependencyMapper.Services;

public interface IDependencyTracer
{
    /// <summary>
    /// Finds all dependency paths from direct dependencies to the specified target package.
    /// </summary>
    TraceResult Trace(DependencyGraph graph, string targetPackage);
}
