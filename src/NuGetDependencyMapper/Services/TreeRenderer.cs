using NuGetDependencyMapper.Models;
using Spectre.Console;

namespace NuGetDependencyMapper.Services;

public class TreeRenderer : ITreeRenderer
{
    public void RenderFullTree(DependencyGraph graph, IAnsiConsole console)
    {
        var tree = new Tree(
            $"[bold blue]{graph.ProjectName.EscapeMarkup()}[/] [dim]({graph.TargetFramework.EscapeMarkup()})[/]");

        var directDeps = graph.DirectDependencies;
        for (int i = 0; i < directDeps.Count; i++)
        {
            AddPackageNode(
                tree,
                directDeps[i],
                graph.Packages,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        console.Write(tree);
    }

    public void RenderTrace(DependencyGraph graph, TraceResult traceResult, IAnsiConsole console)
    {
        var panel = new Panel(
            $"Tracing paths to: [bold green]{traceResult.TargetPackage.EscapeMarkup()}[/] [dim]({traceResult.TargetVersion.EscapeMarkup()})[/]\n" +
            $"Project: [bold blue]{graph.ProjectName.EscapeMarkup()}[/] [dim]({graph.TargetFramework.EscapeMarkup()})[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("blue"));

        console.Write(panel);
        console.WriteLine();

        if (traceResult.Paths.Count == 0)
        {
            console.MarkupLine("[yellow]No dependency paths found.[/]");
            return;
        }

        for (int pathIndex = 0; pathIndex < traceResult.Paths.Count; pathIndex++)
        {
            var path = traceResult.Paths[pathIndex];
            var directLabel = path.IsDirect ? " [dim](direct)[/]" : "";
            console.MarkupLine($"[bold]Path {pathIndex + 1}[/]{directLabel}:");

            var pathTree = new Tree(FormatPackageLabel(
                path.PackageNames[0], graph.Packages));

            IHasTreeNodes currentNode = pathTree;
            for (int depth = 1; depth < path.PackageNames.Count; depth++)
            {
                currentNode = currentNode.AddNode(FormatPackageLabel(
                    path.PackageNames[depth], graph.Packages));
            }

            console.Write(pathTree);
            console.WriteLine();
        }

        console.MarkupLine($"[bold]Total:[/] {traceResult.Paths.Count} path(s) found.");
    }

    private static string FormatPackageLabel(
        string packageName, IReadOnlyDictionary<string, PackageInfo> packages)
    {
        if (packages.TryGetValue(packageName, out var package))
            return $"[green]{package.Name.EscapeMarkup()}[/] [dim]{package.Version.EscapeMarkup()}[/]";

        return $"{packageName.EscapeMarkup()} [dim italic](framework reference)[/]";
    }

    private static void AddPackageNode(
        IHasTreeNodes parent,
        string packageName,
        IReadOnlyDictionary<string, PackageInfo> packages,
        HashSet<string> visited)
    {
        if (!packages.TryGetValue(packageName, out var package))
        {
            parent.AddNode($"{packageName.EscapeMarkup()} [dim italic](framework reference)[/]");
            return;
        }

        var node = parent.AddNode(
            $"[green]{package.Name.EscapeMarkup()}[/] [dim]{package.Version.EscapeMarkup()}[/]");

        if (!visited.Add(packageName))
        {
            node.AddNode("[dim italic](circular reference)[/]");
            return;
        }

        var children = package.Dependencies.Keys
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var child in children)
        {
            AddPackageNode(
                node,
                child,
                packages,
                new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase));
        }
    }
}
