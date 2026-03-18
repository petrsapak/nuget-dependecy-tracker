using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NuGetDependencyMapper.Infrastructure;
using NuGetDependencyMapper.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NuGetDependencyMapper.Cli;

public sealed class MapCommand : Command<MapCommand.Settings>
{
    private readonly IServiceProvider? _serviceProvider;

    public MapCommand() { }

    internal MapCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        [Description("Path to a .csproj or .sln file")]
        public string InputPath { get; set; } = "";

        [CommandOption("-f|--framework <TFM>")]
        [Description("Select a specific target framework")]
        public string? Framework { get; set; }

        [CommandOption("-o|--output <FILE>")]
        [Description("Export the tree to a file")]
        public string? OutputPath { get; set; }

        [CommandOption("-t|--trace <PACKAGE>")]
        [Description("Trace all dependency paths to a specific package")]
        public string? TracePackage { get; set; }

        public bool IsSolution => InputPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

        public override ValidationResult Validate()
        {
            if (!InputPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                !InputPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Error("Input must be a .csproj or .sln file.");
            }

            return ValidationResult.Success();
        }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var services = _serviceProvider ?? BuildServiceProvider();
        var fileSystem = services.GetRequiredService<IFileSystem>();

        var inputPath = Path.GetFullPath(settings.InputPath);
        if (!fileSystem.FileExists(inputPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {inputPath.EscapeMarkup()}");
            return 1;
        }

        var projectPaths = ResolveProjectPaths(inputPath, settings, fileSystem, services);
        if (projectPaths == null)
            return 1;

        if (projectPaths.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No .csproj projects found in the solution.");
            return 1;
        }

        // Set up output console (real console or file-backed)
        IAnsiConsole outputConsole;
        StreamWriter? fileWriter = null;

        if (settings.OutputPath != null)
        {
            var outputFullPath = Path.GetFullPath(settings.OutputPath);
            fileWriter = new StreamWriter(outputFullPath, append: false, encoding: System.Text.Encoding.UTF8);
            outputConsole = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Out = new AnsiConsoleOutput(fileWriter)
            });
        }
        else
        {
            outputConsole = AnsiConsole.Console;
        }

        try
        {
            if (settings.IsSolution)
            {
                outputConsole.MarkupLine($"[blue]Solution:[/] {Path.GetFileName(inputPath).EscapeMarkup()} — [bold]{projectPaths.Count}[/] project(s) found");
                outputConsole.WriteLine();
            }

            var graphBuilder = services.GetRequiredService<IDependencyGraphBuilder>();
            var renderer = services.GetRequiredService<ITreeRenderer>();
            var tracer = services.GetRequiredService<IDependencyTracer>();
            int processedCount = 0;
            int skippedCount = 0;

            for (int p = 0; p < projectPaths.Count; p++)
            {
                var csprojPath = projectPaths[p];
                var projectName = Path.GetFileNameWithoutExtension(csprojPath);
                var projectDirectory = Path.GetDirectoryName(csprojPath)!;
                var assetsFilePath = Path.Combine(projectDirectory, "obj", "project.assets.json");

                if (!fileSystem.FileExists(assetsFilePath))
                {
                    if (settings.IsSolution)
                    {
                        outputConsole.MarkupLine($"[yellow]Skipping[/] {projectName.EscapeMarkup()}: project.assets.json not found [dim](run dotnet restore)[/]");
                        skippedCount++;
                        continue;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] project.assets.json not found at {assetsFilePath.EscapeMarkup()}");
                        AnsiConsole.MarkupLine("Run [bold]dotnet restore[/] first.");
                        return 1;
                    }
                }

                var assetsJsonContent = fileSystem.ReadAllText(assetsFilePath);

                try
                {
                    var graph = graphBuilder.Build(projectName, assetsJsonContent, settings.Framework);

                    if (graph.DirectDependencies.Count == 0)
                    {
                        if (settings.IsSolution)
                        {
                            outputConsole.MarkupLine($"[yellow]Skipping[/] {projectName.EscapeMarkup()}: no package references found");
                        }
                        else
                        {
                            outputConsole.MarkupLine($"[dim]{projectName.EscapeMarkup()} has no package references.[/]");
                        }
                        skippedCount++;
                        continue;
                    }

                    if (settings.TracePackage != null)
                    {
                        var traceResult = tracer.Trace(graph, settings.TracePackage);

                        if (settings.IsSolution && traceResult.Paths.Count == 0)
                        {
                            skippedCount++;
                            continue;
                        }

                        if (processedCount > 0)
                        {
                            outputConsole.WriteLine();
                            outputConsole.Write(new Rule().RuleStyle(Style.Parse("dim")));
                            outputConsole.WriteLine();
                        }

                        renderer.RenderTrace(graph, traceResult, outputConsole);
                    }
                    else
                    {
                        if (processedCount > 0)
                        {
                            outputConsole.WriteLine();
                            outputConsole.Write(new Rule().RuleStyle(Style.Parse("dim")));
                            outputConsole.WriteLine();
                        }

                        if (graph.AvailableFrameworks.Count > 1 && settings.Framework == null && !settings.IsSolution)
                        {
                            outputConsole.MarkupLine(
                                $"[dim]Multiple frameworks found: {string.Join(", ", graph.AvailableFrameworks).EscapeMarkup()}. Showing: [bold]{graph.TargetFramework.EscapeMarkup()}[/][/]");
                            outputConsole.MarkupLine("[dim]Use --framework <tfm> to select a different one[/]");
                            outputConsole.WriteLine();
                        }

                        renderer.RenderFullTree(graph, outputConsole);
                    }

                    processedCount++;
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException)
                {
                    if (settings.IsSolution)
                    {
                        outputConsole.MarkupLine($"[yellow]Skipping[/] {projectName.EscapeMarkup()}: {ex.Message.EscapeMarkup()}");
                        skippedCount++;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
                        return 1;
                    }
                }
            }

            if (settings.IsSolution)
            {
                outputConsole.WriteLine();
                outputConsole.Write(new Rule($"[blue]{processedCount} project(s) processed, {skippedCount} skipped[/]")
                    .RuleStyle(Style.Parse("dim")));
            }
        }
        finally
        {
            fileWriter?.Dispose();
        }

        if (settings.OutputPath != null)
        {
            AnsiConsole.MarkupLine($"[green]Dependency tree exported to:[/] {Path.GetFullPath(settings.OutputPath).EscapeMarkup()}");
        }

        return 0;
    }

    private static List<string>? ResolveProjectPaths(
        string inputPath, Settings settings, IFileSystem fileSystem, IServiceProvider serviceProvider)
    {
        if (settings.IsSolution)
        {
            var solutionContent = fileSystem.ReadAllText(inputPath);
            var solutionParser = serviceProvider.GetRequiredService<ISolutionParser>();
            var projectPaths = solutionParser.GetProjectPaths(inputPath, solutionContent);

            return projectPaths.ToList();
        }

        if (!inputPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Input must be a .csproj or .sln file.");
            return null;
        }

        return [inputPath];
    }

    private static ServiceProvider BuildServiceProvider()
    {
        return new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<ITargetFrameworkResolver, TargetFrameworkResolver>()
            .AddSingleton<IAssetsFileParser, AssetsFileParser>()
            .AddSingleton<IDependencyGraphBuilder, DependencyGraphBuilder>()
            .AddSingleton<IDependencyTracer, DependencyTracer>()
            .AddSingleton<ITreeRenderer, TreeRenderer>()
            .AddSingleton<ISolutionParser, SolutionParser>()
            .BuildServiceProvider();
    }
}
