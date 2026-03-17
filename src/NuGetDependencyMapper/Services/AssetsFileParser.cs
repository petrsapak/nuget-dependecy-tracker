using System.Text.Json;
using NuGetDependencyMapper.Models;

namespace NuGetDependencyMapper.Services;

public class AssetsFileParser : IAssetsFileParser
{
    public AssetsFileContent Parse(string assetsJsonContent)
    {
        using var document = JsonDocument.Parse(assetsJsonContent);
        var root = document.RootElement;

        var availableFrameworks = ParseAvailableFrameworks(root);
        var directDependenciesByFramework = ParseDirectDependencies(root, availableFrameworks);
        var (availableTargets, packagesByTarget) = ParseTargets(root);

        return new AssetsFileContent
        {
            AvailableFrameworks = availableFrameworks,
            DirectDependenciesByFramework = directDependenciesByFramework,
            PackagesByTarget = packagesByTarget,
            AvailableTargets = availableTargets
        };
    }

    private static List<string> ParseAvailableFrameworks(JsonElement root)
    {
        var frameworks = root.GetProperty("project").GetProperty("frameworks");
        return frameworks.EnumerateObject().Select(f => f.Name).ToList();
    }

    private static Dictionary<string, IReadOnlyList<string>> ParseDirectDependencies(
        JsonElement root, IReadOnlyList<string> frameworks)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var frameworksElement = root.GetProperty("project").GetProperty("frameworks");

        foreach (var tfm in frameworks)
        {
            var directDeps = new List<string>();
            var frameworkSection = frameworksElement.GetProperty(tfm);

            if (frameworkSection.TryGetProperty("dependencies", out var dependencies))
            {
                foreach (var dep in dependencies.EnumerateObject())
                {
                    if (IsPackageReference(dep.Value))
                        directDeps.Add(dep.Name);
                }
            }

            result[tfm] = directDeps;
        }

        return result;
    }

    private static bool IsPackageReference(JsonElement dependencyValue)
    {
        if (dependencyValue.TryGetProperty("target", out var target))
            return target.GetString()?.Equals("Package", StringComparison.OrdinalIgnoreCase) == true;

        // If no target specified, assume it's a package
        return true;
    }

    private static (List<string> AvailableTargets, Dictionary<string, IReadOnlyDictionary<string, PackageInfo>> PackagesByTarget) ParseTargets(
        JsonElement root)
    {
        var targets = root.GetProperty("targets");
        var availableTargets = new List<string>();
        var packagesByTarget = new Dictionary<string, IReadOnlyDictionary<string, PackageInfo>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var targetEntry in targets.EnumerateObject())
        {
            availableTargets.Add(targetEntry.Name);
            var packages = ParsePackagesInTarget(targetEntry.Value);
            packagesByTarget[targetEntry.Name] = packages;
        }

        return (availableTargets, packagesByTarget);
    }

    private static Dictionary<string, PackageInfo> ParsePackagesInTarget(JsonElement targetElement)
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in targetElement.EnumerateObject())
        {
            if (IsProjectReference(entry.Value))
                continue;

            var slashIndex = entry.Name.IndexOf('/');
            if (slashIndex < 0)
                continue;

            var packageName = entry.Name[..slashIndex];
            var packageVersion = entry.Name[(slashIndex + 1)..];
            var dependencies = ParsePackageDependencies(entry.Value);

            packages[packageName] = new PackageInfo(packageName, packageVersion, dependencies);
        }

        return packages;
    }

    private static bool IsProjectReference(JsonElement packageElement)
    {
        return packageElement.TryGetProperty("type", out var type) &&
               type.GetString()?.Equals("project", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Dictionary<string, string> ParsePackageDependencies(JsonElement packageElement)
    {
        var dependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (packageElement.TryGetProperty("dependencies", out var depsElement))
        {
            foreach (var dep in depsElement.EnumerateObject())
            {
                dependencies[dep.Name] = dep.Value.GetString() ?? "";
            }
        }

        return dependencies;
    }
}
