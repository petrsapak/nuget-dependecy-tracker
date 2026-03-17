using NuGetDependencyMapper.Models;
using NuGetDependencyMapper.Services;

namespace NuGetDependencyMapper.Tests.Services;

public class DependencyTracerTests
{
    private readonly DependencyTracer _tracer = new();

    private static DependencyGraph CreateGraph(
        Dictionary<string, PackageInfo> packages,
        List<string> directDeps)
    {
        return new DependencyGraph
        {
            ProjectName = "TestProject",
            TargetFramework = "net8.0",
            DirectDependencies = directDeps,
            Packages = packages,
            AvailableFrameworks = ["net8.0"]
        };
    }

    [Fact]
    public void Trace_DirectDependency_ReturnsSingleElementPath()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA"]);

        var result = _tracer.Trace(graph, "PackageA");

        Assert.Single(result.Paths);
        Assert.True(result.Paths[0].IsDirect);
        Assert.Equal(new[] { "PackageA" }, result.Paths[0].PackageNames);
    }

    [Fact]
    public void Trace_TransitiveDependency_FindsPath()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string> { ["PackageB"] = "2.0.0" }),
            ["PackageB"] = new("PackageB", "2.0.0", new Dictionary<string, string> { ["PackageC"] = "3.0.0" }),
            ["PackageC"] = new("PackageC", "3.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA"]);

        var result = _tracer.Trace(graph, "PackageC");

        Assert.Single(result.Paths);
        Assert.False(result.Paths[0].IsDirect);
        Assert.Equal(new[] { "PackageA", "PackageB", "PackageC" }, result.Paths[0].PackageNames);
    }

    [Fact]
    public void Trace_MultiplePaths_FindsAll()
    {
        // A -> C (direct path) and A -> B -> C (transitive path)
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>
            {
                ["PackageB"] = "2.0.0",
                ["PackageC"] = "3.0.0"
            }),
            ["PackageB"] = new("PackageB", "2.0.0", new Dictionary<string, string> { ["PackageC"] = "3.0.0" }),
            ["PackageC"] = new("PackageC", "3.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA"]);

        var result = _tracer.Trace(graph, "PackageC");

        Assert.Equal(2, result.Paths.Count);
    }

    [Fact]
    public void Trace_MultipleDirectDepsLeadToTarget_FindsAllPaths()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string> { ["log4net"] = "2.0.10" }),
            ["PackageB"] = new("PackageB", "2.0.0", new Dictionary<string, string> { ["log4net"] = "2.0.10" }),
            ["log4net"] = new("log4net", "2.0.10", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA", "PackageB"]);

        var result = _tracer.Trace(graph, "log4net");

        Assert.Equal(2, result.Paths.Count);
        Assert.Equal(new[] { "PackageA", "log4net" }, result.Paths[0].PackageNames);
        Assert.Equal(new[] { "PackageB", "log4net" }, result.Paths[1].PackageNames);
    }

    [Fact]
    public void Trace_PackageNotInGraph_ThrowsWithMessage()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["PackageA"]);

        var ex = Assert.Throws<ArgumentException>(() => _tracer.Trace(graph, "NonExistent"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Trace_PackageNotInGraph_SuggestsSimilarNames()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.Data.SqlClient"] = new("Microsoft.Data.SqlClient", "5.0.0", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["Microsoft.Data.SqlClient"]);

        var ex = Assert.Throws<ArgumentException>(() => _tracer.Trace(graph, "SqlClient"));
        Assert.Contains("Microsoft.Data.SqlClient", ex.Message);
    }

    [Fact]
    public void Trace_CircularDependency_DoesNotInfiniteLoop()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string> { ["PackageB"] = "2.0.0" }),
            ["PackageB"] = new("PackageB", "2.0.0", new Dictionary<string, string> { ["PackageA"] = "1.0.0" })
        };
        var graph = CreateGraph(packages, ["PackageA"]);

        // Should complete without hanging; no path to a non-existent target through the cycle
        var result = _tracer.Trace(graph, "PackageB");
        Assert.NotEmpty(result.Paths);
    }

    [Fact]
    public void Trace_SetsTargetPackageAndVersion()
    {
        var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["log4net"] = new("log4net", "2.0.10", new Dictionary<string, string>())
        };
        var graph = CreateGraph(packages, ["log4net"]);

        var result = _tracer.Trace(graph, "log4net");

        Assert.Equal("log4net", result.TargetPackage);
        Assert.Equal("2.0.10", result.TargetVersion);
    }
}
