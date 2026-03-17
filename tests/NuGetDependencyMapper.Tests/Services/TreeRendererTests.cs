using NuGetDependencyMapper.Models;
using NuGetDependencyMapper.Services;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NuGetDependencyMapper.Tests.Services;

public class TreeRendererTests
{
    private readonly TreeRenderer _renderer = new();

    private static DependencyGraph CreateGraph(
        Dictionary<string, PackageInfo> packages,
        List<string> directDeps,
        string tfm = "net8.0")
    {
        return new DependencyGraph
        {
            ProjectName = "TestProject",
            TargetFramework = tfm,
            DirectDependencies = directDeps,
            Packages = packages,
            AvailableFrameworks = [tfm]
        };
    }

    private static TestConsole CreateTestConsole()
    {
        var console = new TestConsole();
        console.Profile.Width = 120;
        return console;
    }

    [Fact]
    public void RenderFullTree_ProjectHeader_IncludesNameAndFramework()
    {
        var graph = CreateGraph(new Dictionary<string, PackageInfo>(), [], "net48");
        var console = CreateTestConsole();

        _renderer.RenderFullTree(graph, console);

        Assert.Contains("TestProject", console.Output);
        Assert.Contains("net48", console.Output);
    }

    [Fact]
    public void RenderFullTree_SinglePackageNoDeps_RendersLeafNode()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA"]);
        var console = CreateTestConsole();

        _renderer.RenderFullTree(graph, console);

        Assert.Contains("PackageA", console.Output);
        Assert.Contains("1.0.0", console.Output);
    }

    [Fact]
    public void RenderFullTree_MultipleDirectDeps_RendersBothPackages()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>()),
            ["PackageB"] = new("PackageB", "2.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA", "PackageB"]);
        var console = CreateTestConsole();

        _renderer.RenderFullTree(graph, console);
        var output = console.Output;

        Assert.Contains("PackageA", output);
        Assert.Contains("1.0.0", output);
        Assert.Contains("PackageB", output);
        Assert.Contains("2.0.0", output);
    }

    [Fact]
    public void RenderFullTree_NestedDeps_RendersChildNodes()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Parent"] = new("Parent", "1.0.0", new Dictionary<string, string> { ["Child"] = "2.0.0" }),
            ["Child"] = new("Child", "2.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["Parent"]);
        var console = CreateTestConsole();

        _renderer.RenderFullTree(graph, console);
        var output = console.Output;

        Assert.Contains("TestProject", output);
        Assert.Contains("Parent", output);
        Assert.Contains("1.0.0", output);
        Assert.Contains("Child", output);
        Assert.Contains("2.0.0", output);
    }

    [Fact]
    public void RenderFullTree_FrameworkReference_ShowsLabel()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Parent"] = new("Parent", "1.0.0", new Dictionary<string, string> { ["MissingPkg"] = "1.0.0" })
        };
        var graph = CreateGraph(packages, ["Parent"]);
        var console = CreateTestConsole();

        _renderer.RenderFullTree(graph, console);

        Assert.Contains("MissingPkg", console.Output);
        Assert.Contains("framework reference", console.Output);
    }

    [Fact]
    public void RenderTrace_NoPathsFound_PrintsMessage()
    {
        var graph = CreateGraph(new Dictionary<string, PackageInfo>(), []);
        var traceResult = new TraceResult
        {
            TargetPackage = "PackageX",
            TargetVersion = "1.0.0",
            Paths = new List<DependencyPath>()
        };
        var console = CreateTestConsole();

        _renderer.RenderTrace(graph, traceResult, console);

        Assert.Contains("No dependency paths found", console.Output);
    }

    [Fact]
    public void RenderTrace_HeaderIncludesTargetInfo()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["log4net"] = new("log4net", "2.0.10", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["log4net"]);
        var traceResult = new TraceResult
        {
            TargetPackage = "log4net",
            TargetVersion = "2.0.10",
            Paths = new List<DependencyPath>
            {
                new() { PackageNames = new List<string> { "log4net" } }
            }
        };
        var console = CreateTestConsole();

        _renderer.RenderTrace(graph, traceResult, console);
        var output = console.Output;

        Assert.Contains("log4net", output);
        Assert.Contains("2.0.10", output);
        Assert.Contains("TestProject", output);
    }

    [Fact]
    public void RenderTrace_DirectPath_ShowsDirectLabel()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["log4net"] = new("log4net", "2.0.10", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["log4net"]);
        var traceResult = new TraceResult
        {
            TargetPackage = "log4net",
            TargetVersion = "2.0.10",
            Paths = new List<DependencyPath>
            {
                new() { PackageNames = new List<string> { "log4net" } }
            }
        };
        var console = CreateTestConsole();

        _renderer.RenderTrace(graph, traceResult, console);

        Assert.Contains("direct", console.Output);
    }

    [Fact]
    public void RenderTrace_ShowsTotalPathCount()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new("A", "1.0", new Dictionary<string, string>()),
            ["B"] = new("B", "1.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["A", "B"]);
        var traceResult = new TraceResult
        {
            TargetPackage = "Target",
            TargetVersion = "1.0",
            Paths = new List<DependencyPath>
            {
                new() { PackageNames = new List<string> { "A", "Target" } },
                new() { PackageNames = new List<string> { "B", "Target" } }
            }
        };
        var console = CreateTestConsole();

        _renderer.RenderTrace(graph, traceResult, console);

        Assert.Contains("2 path(s) found", console.Output);
    }
}
