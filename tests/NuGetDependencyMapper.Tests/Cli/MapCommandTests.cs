using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuGetDependencyMapper.Cli;
using NuGetDependencyMapper.Infrastructure;
using NuGetDependencyMapper.Models;
using NuGetDependencyMapper.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NuGetDependencyMapper.Tests.Cli;

public class MapCommandTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IDependencyGraphBuilder _graphBuilder = Substitute.For<IDependencyGraphBuilder>();
    private readonly IDependencyTracer _tracer = Substitute.For<IDependencyTracer>();
    private readonly ITreeRenderer _renderer = Substitute.For<ITreeRenderer>();
    private readonly ISolutionParser _solutionParser = Substitute.For<ISolutionParser>();

    private MapCommand CreateCommand()
    {
        var services = new ServiceCollection()
            .AddSingleton(_fileSystem)
            .AddSingleton(_graphBuilder)
            .AddSingleton(_tracer)
            .AddSingleton(_renderer)
            .AddSingleton(_solutionParser)
            .BuildServiceProvider();

        return new MapCommand(services);
    }

    // CommandContext is not used by MapCommand's logic, so we pass null.
    private static CommandContext CreateContext() => null!;

    private static DependencyGraph CreateGraph(
        string projectName = "TestProject",
        string tfm = "net8.0",
        List<string>? directDeps = null,
        Dictionary<string, PackageInfo>? packages = null,
        List<string>? availableFrameworks = null)
    {
        return new DependencyGraph
        {
            ProjectName = projectName,
            TargetFramework = tfm,
            DirectDependencies = directDeps ?? ["PackageA"],
            Packages = packages ?? new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>())
            },
            AvailableFrameworks = availableFrameworks ?? [tfm]
        };
    }

    // Use a real csproj path that matches the filesystem mock
    private string SetupSingleProject(string projectName = "TestProject")
    {
        var csprojPath = Path.GetFullPath($"{projectName}.csproj");
        var assetsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(csprojPath).Returns(true);
        _fileSystem.FileExists(assetsPath).Returns(true);
        _fileSystem.ReadAllText(assetsPath).Returns("{}");

        return csprojPath;
    }

    #region File and path validation

    [Fact]
    public void Execute_FileNotFound_ReturnsError()
    {
        var command = CreateCommand();
        var csprojPath = Path.GetFullPath("Missing.csproj");
        _fileSystem.FileExists(csprojPath).Returns(false);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath
        });

        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_MissingAssetsFile_Project_ReturnsError()
    {
        var command = CreateCommand();
        var csprojPath = Path.GetFullPath("Test.csproj");
        var assetsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(csprojPath).Returns(true);
        _fileSystem.FileExists(assetsPath).Returns(false);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath
        });

        Assert.Equal(1, result);
    }

    #endregion

    #region Error handling — ArgumentException

    [Fact]
    public void Execute_InvalidFramework_Project_ReturnsError()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        _graphBuilder
            .Build(Arg.Any<string>(), Arg.Any<string>(), "badframework")
            .Returns(_ => throw new ArgumentException("Framework 'badframework' not found."));

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath,
            Framework = "badframework"
        });

        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_InvalidTracePackage_Project_ReturnsError()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        var graph = CreateGraph();
        _graphBuilder.Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(graph);
        _tracer.Trace(graph, "NonExistent")
            .Returns(_ => throw new ArgumentException("Package 'NonExistent' not found."));

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath,
            TracePackage = "NonExistent"
        });

        Assert.Equal(1, result);
    }

    #endregion

    #region Error handling — InvalidOperationException

    [Fact]
    public void Execute_InvalidOperationException_Project_ReturnsError()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        _graphBuilder
            .Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new InvalidOperationException("No targets section found."));

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath
        });

        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_InvalidOperationException_Solution_SkipsProject()
    {
        var command = CreateCommand();
        var slnPath = Path.GetFullPath("Test.sln");
        var csprojPath = Path.GetFullPath("ProjectA.csproj");
        var assetsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(slnPath).Returns(true);
        _fileSystem.ReadAllText(slnPath).Returns("sln content");
        _solutionParser.GetProjectPaths(slnPath, "sln content").Returns([csprojPath]);
        _fileSystem.FileExists(assetsPath).Returns(true);
        _fileSystem.ReadAllText(assetsPath).Returns("{}");

        _graphBuilder
            .Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new InvalidOperationException("No targets section found."));

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = slnPath
        });

        // Solution mode should skip, not fail
        Assert.Equal(0, result);
    }

    #endregion

    #region Error handling — JsonException (malformed assets file)

    [Fact]
    public void Execute_MalformedJson_Project_ReturnsError()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        _graphBuilder
            .Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new JsonException("'x' is invalid JSON."));

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath
        });

        Assert.Equal(1, result);
    }

    [Fact]
    public void Execute_MalformedJson_Solution_SkipsProject()
    {
        var command = CreateCommand();
        var slnPath = Path.GetFullPath("Test.sln");
        var csprojPath = Path.GetFullPath("ProjectA.csproj");
        var assetsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(slnPath).Returns(true);
        _fileSystem.ReadAllText(slnPath).Returns("sln content");
        _solutionParser.GetProjectPaths(slnPath, "sln content").Returns([csprojPath]);
        _fileSystem.FileExists(assetsPath).Returns(true);
        _fileSystem.ReadAllText(assetsPath).Returns("not json");

        _graphBuilder
            .Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new JsonException("Malformed JSON."));

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = slnPath
        });

        Assert.Equal(0, result);
    }

    #endregion

    #region Zero-dependency projects

    [Fact]
    public void Execute_ZeroDeps_Project_ReturnsSuccessWithMessage()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        var graph = CreateGraph(directDeps: []);
        _graphBuilder.Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(graph);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath
        });

        Assert.Equal(0, result);
        // Renderer should not have been called
        _renderer.DidNotReceive().RenderFullTree(Arg.Any<DependencyGraph>(), Arg.Any<IAnsiConsole>());
    }

    #endregion

    #region Successful execution

    [Fact]
    public void Execute_ValidProject_CallsRendererAndReturnsSuccess()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        var graph = CreateGraph();
        _graphBuilder.Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(graph);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath
        });

        Assert.Equal(0, result);
        _renderer.Received(1).RenderFullTree(graph, Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Execute_ValidProjectWithTrace_CallsTracerAndRenderer()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        var graph = CreateGraph();
        _graphBuilder.Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(graph);

        var traceResult = new TraceResult
        {
            TargetPackage = "PackageA",
            TargetVersion = "1.0.0",
            Paths = new List<DependencyPath>
            {
                new() { PackageNames = ["PackageA"] }
            }
        };
        _tracer.Trace(graph, "PackageA").Returns(traceResult);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath,
            TracePackage = "PackageA"
        });

        Assert.Equal(0, result);
        _renderer.Received(1).RenderTrace(graph, traceResult, Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Execute_ValidProject_PassesFrameworkToBuilder()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();

        var graph = CreateGraph();
        _graphBuilder.Build(Arg.Any<string>(), Arg.Any<string>(), "net48").Returns(graph);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = csprojPath,
            Framework = "net48"
        });

        Assert.Equal(0, result);
        _graphBuilder.Received(1).Build(Arg.Any<string>(), Arg.Any<string>(), "net48");
    }

    #endregion

    #region Output file export

    [Fact]
    public void Execute_WithOutputPath_CreatesFile()
    {
        var command = CreateCommand();
        var csprojPath = SetupSingleProject();
        var outputPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}.txt"));

        var graph = CreateGraph();
        _graphBuilder.Build(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns(graph);

        try
        {
            var result = command.Execute(CreateContext(), new MapCommand.Settings
            {
                InputPath = csprojPath,
                OutputPath = outputPath
            });

            Assert.Equal(0, result);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    #endregion

    #region Solution mode

    [Fact]
    public void Execute_Solution_ProcessesMultipleProjects()
    {
        var command = CreateCommand();
        var slnPath = Path.GetFullPath("Multi.sln");
        var projAPath = Path.GetFullPath(Path.Combine("projA", "ProjectA.csproj"));
        var projBPath = Path.GetFullPath(Path.Combine("projB", "ProjectB.csproj"));
        var assetsA = Path.Combine(Path.GetDirectoryName(projAPath)!, "obj", "project.assets.json");
        var assetsB = Path.Combine(Path.GetDirectoryName(projBPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(slnPath).Returns(true);
        _fileSystem.ReadAllText(slnPath).Returns("sln content");
        _solutionParser.GetProjectPaths(slnPath, "sln content").Returns([projAPath, projBPath]);
        _fileSystem.FileExists(assetsA).Returns(true);
        _fileSystem.FileExists(assetsB).Returns(true);
        _fileSystem.ReadAllText(assetsA).Returns("{}");
        _fileSystem.ReadAllText(assetsB).Returns("{}");

        var graphA = CreateGraph(projectName: "ProjectA");
        var graphB = CreateGraph(projectName: "ProjectB");

        _graphBuilder.Build("ProjectA", Arg.Any<string>(), Arg.Any<string?>()).Returns(graphA);
        _graphBuilder.Build("ProjectB", Arg.Any<string>(), Arg.Any<string?>()).Returns(graphB);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = slnPath
        });

        Assert.Equal(0, result);
        _renderer.Received(1).RenderFullTree(graphA, Arg.Any<IAnsiConsole>());
        _renderer.Received(1).RenderFullTree(graphB, Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Execute_Solution_MissingAssets_SkipsAndContinues()
    {
        var command = CreateCommand();
        var slnPath = Path.GetFullPath("Partial.sln");
        var projAPath = Path.GetFullPath(Path.Combine("projA", "ProjectA.csproj"));
        var projBPath = Path.GetFullPath(Path.Combine("projB", "ProjectB.csproj"));
        var assetsA = Path.Combine(Path.GetDirectoryName(projAPath)!, "obj", "project.assets.json");
        var assetsB = Path.Combine(Path.GetDirectoryName(projBPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(slnPath).Returns(true);
        _fileSystem.ReadAllText(slnPath).Returns("sln content");
        _solutionParser.GetProjectPaths(slnPath, "sln content").Returns([projAPath, projBPath]);
        _fileSystem.FileExists(assetsA).Returns(false); // missing
        _fileSystem.FileExists(assetsB).Returns(true);
        _fileSystem.ReadAllText(assetsB).Returns("{}");

        var graphB = CreateGraph(projectName: "ProjectB");
        _graphBuilder.Build("ProjectB", Arg.Any<string>(), Arg.Any<string?>()).Returns(graphB);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = slnPath
        });

        Assert.Equal(0, result);
        // Only ProjectB should have been rendered
        _graphBuilder.DidNotReceive().Build("ProjectA", Arg.Any<string>(), Arg.Any<string?>());
        _renderer.Received(1).RenderFullTree(graphB, Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Execute_Solution_ArgumentException_SkipsAndContinues()
    {
        var command = CreateCommand();
        var slnPath = Path.GetFullPath("Mixed.sln");
        var projAPath = Path.GetFullPath(Path.Combine("projA", "ProjectA.csproj"));
        var projBPath = Path.GetFullPath(Path.Combine("projB", "ProjectB.csproj"));
        var assetsA = Path.Combine(Path.GetDirectoryName(projAPath)!, "obj", "project.assets.json");
        var assetsB = Path.Combine(Path.GetDirectoryName(projBPath)!, "obj", "project.assets.json");

        _fileSystem.FileExists(slnPath).Returns(true);
        _fileSystem.ReadAllText(slnPath).Returns("sln content");
        _solutionParser.GetProjectPaths(slnPath, "sln content").Returns([projAPath, projBPath]);
        _fileSystem.FileExists(assetsA).Returns(true);
        _fileSystem.FileExists(assetsB).Returns(true);
        _fileSystem.ReadAllText(assetsA).Returns("{}");
        _fileSystem.ReadAllText(assetsB).Returns("{}");

        _graphBuilder.Build("ProjectA", Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_ => throw new ArgumentException("Framework not found."));

        var graphB = CreateGraph(projectName: "ProjectB");
        _graphBuilder.Build("ProjectB", Arg.Any<string>(), Arg.Any<string?>()).Returns(graphB);

        var result = command.Execute(CreateContext(), new MapCommand.Settings
        {
            InputPath = slnPath
        });

        Assert.Equal(0, result);
        _renderer.Received(1).RenderFullTree(graphB, Arg.Any<IAnsiConsole>());
    }

    #endregion

    #region Settings validation

    [Theory]
    [InlineData("test.csproj")]
    [InlineData("test.sln")]
    public void Settings_Validate_AcceptsValidExtensions(string path)
    {
        var settings = new MapCommand.Settings { InputPath = path };
        var result = settings.Validate();
        Assert.True(result.Successful);
    }

    [Theory]
    [InlineData("test.txt")]
    [InlineData("test.json")]
    [InlineData("test")]
    public void Settings_Validate_RejectsInvalidExtensions(string path)
    {
        var settings = new MapCommand.Settings { InputPath = path };
        var result = settings.Validate();
        Assert.False(result.Successful);
    }

    [Theory]
    [InlineData("test.sln", true)]
    [InlineData("test.csproj", false)]
    public void Settings_IsSolution_ReturnsCorrectly(string path, bool expected)
    {
        var settings = new MapCommand.Settings { InputPath = path };
        Assert.Equal(expected, settings.IsSolution);
    }

    #endregion
}
