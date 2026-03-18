using NuGetDependencyMapper.Models;

namespace NuGetDependencyMapper.Services;

public class DependencyGraphBuilder : IDependencyGraphBuilder
{
    private readonly IAssetsFileParser _assetsFileParser;
    private readonly ITargetFrameworkResolver _frameworkResolver;

    public DependencyGraphBuilder(
        IAssetsFileParser assetsFileParser,
        ITargetFrameworkResolver frameworkResolver)
    {
        _assetsFileParser = assetsFileParser;
        _frameworkResolver = frameworkResolver;
    }

    public DependencyGraph Build(string projectName, string assetsJsonContent, string? requestedFramework)
    {
        var assetsContent = _assetsFileParser.Parse(assetsJsonContent);

        if (assetsContent.AvailableFrameworks.Count == 0)
            throw new InvalidOperationException("No target frameworks found in project.assets.json.");

        var selectedFramework = SelectFramework(assetsContent, requestedFramework);
        var targetsKey = ResolveTargetsKey(selectedFramework, assetsContent);

        var directDeps = assetsContent.DirectDependenciesByFramework.TryGetValue(selectedFramework, out var deps)
            ? deps.OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

        var packages = assetsContent.PackagesByTarget.TryGetValue(targetsKey, out var pkgs)
            ? pkgs
            : new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);

        return new DependencyGraph
        {
            ProjectName = projectName,
            TargetFramework = selectedFramework,
            DirectDependencies = directDeps,
            Packages = packages,
            AvailableFrameworks = assetsContent.AvailableFrameworks
        };
    }

    private static string SelectFramework(AssetsFileContent content, string? requestedFramework)
    {
        if (requestedFramework == null)
            return content.AvailableFrameworks[0];

        // 1. Exact match
        var match = content.AvailableFrameworks.FirstOrDefault(
            t => t.Equals(requestedFramework, StringComparison.OrdinalIgnoreCase));

        // 2. Substring match
        match ??= content.AvailableFrameworks.FirstOrDefault(
            t => t.Contains(requestedFramework, StringComparison.OrdinalIgnoreCase));

        // 3. Alias normalization: convert short alias (e.g. "net48") to long form
        //    and match against available frameworks
        if (match == null)
        {
            var longForm = TargetFrameworkResolver.ConvertToLongFormMoniker(requestedFramework);
            if (longForm != null)
            {
                match = content.AvailableFrameworks.FirstOrDefault(
                    t => t.Equals(longForm, StringComparison.OrdinalIgnoreCase));

                match ??= content.AvailableFrameworks.FirstOrDefault(
                    t => t.Contains(longForm, StringComparison.OrdinalIgnoreCase));
            }
        }

        // 4. Reverse: the request might be long-form, try matching against shorter framework names
        match ??= content.AvailableFrameworks.FirstOrDefault(
            t => requestedFramework.Contains(t, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            var available = string.Join(", ", content.AvailableFrameworks);
            throw new ArgumentException(
                $"Framework '{requestedFramework}' not found. Available: {available}");
        }

        return match;
    }

    private string ResolveTargetsKey(string frameworkMoniker, AssetsFileContent content)
    {
        var targetsKey = _frameworkResolver.ResolveTargetsKey(
            frameworkMoniker, content.AvailableTargets);

        if (targetsKey == null)
        {
            var available = string.Join(", ", content.AvailableTargets);
            throw new InvalidOperationException(
                $"No targets section found for framework '{frameworkMoniker}'. Available targets: {available}");
        }

        return targetsKey;
    }
}
