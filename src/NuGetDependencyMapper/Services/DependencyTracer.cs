using NuGetDependencyMapper.Models;

namespace NuGetDependencyMapper.Services;

public class DependencyTracer : IDependencyTracer
{
    public TraceResult Trace(DependencyGraph graph, string targetPackage)
    {
        if (!graph.Packages.TryGetValue(targetPackage, out var targetInfo))
        {
            var suggestions = FindSimilarPackageNames(graph.Packages, targetPackage);
            var message = $"Package '{targetPackage}' not found in the dependency graph.";
            if (suggestions.Count > 0)
                message += $" Did you mean: {string.Join(", ", suggestions)}?";

            throw new ArgumentException(message);
        }

        var allPaths = new List<DependencyPath>();

        foreach (var directDep in graph.DirectDependencies)
        {
            var pathsFromDirect = new List<List<string>>();
            FindPathsRecursive(
                directDep,
                targetPackage,
                graph.Packages,
                currentPath: new List<string>(),
                results: pathsFromDirect,
                visited: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            allPaths.AddRange(pathsFromDirect.Select(p => new DependencyPath
            {
                PackageNames = p.AsReadOnly()
            }));
        }

        return new TraceResult
        {
            TargetPackage = targetInfo.Name,
            TargetVersion = targetInfo.Version,
            Paths = allPaths.AsReadOnly()
        };
    }

    private static void FindPathsRecursive(
        string currentPackage,
        string targetPackage,
        IReadOnlyDictionary<string, PackageInfo> packages,
        List<string> currentPath,
        List<List<string>> results,
        HashSet<string> visited)
    {
        if (!packages.TryGetValue(currentPackage, out var packageInfo))
            return;

        currentPath.Add(currentPackage);

        if (currentPackage.Equals(targetPackage, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new List<string>(currentPath));
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        if (!visited.Add(currentPackage))
        {
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        foreach (var dependency in packageInfo.Dependencies.Keys)
        {
            FindPathsRecursive(dependency, targetPackage, packages, currentPath, results, visited);
        }

        visited.Remove(currentPackage);
        currentPath.RemoveAt(currentPath.Count - 1);
    }

    private static List<string> FindSimilarPackageNames(
        IReadOnlyDictionary<string, PackageInfo> packages, string searchTerm)
    {
        return packages.Keys
            .Where(k => k.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }
}
