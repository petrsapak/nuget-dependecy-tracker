using NSubstitute;
using NuGetDependencyMapper.Models;
using NuGetDependencyMapper.Services;

namespace NuGetDependencyMapper.Tests.Services;

public class DependencyGraphBuilderTests
{
    private readonly IAssetsFileParser _parser = Substitute.For<IAssetsFileParser>();
    private readonly ITargetFrameworkResolver _resolver = Substitute.For<ITargetFrameworkResolver>();
    private readonly DependencyGraphBuilder _builder;

    // Use realistic long-form framework names as produced by the AssetsFileParser
    // (project.frameworks keys in real project.assets.json files)
    private static readonly AssetsFileContent SampleContent = new()
    {
        AvailableFrameworks = ["net8.0", ".NETFramework,Version=v4.8"],
        DirectDependenciesByFramework = new Dictionary<string, IReadOnlyList<string>>
        {
            ["net8.0"] = new List<string> { "PackageB", "PackageA" },
            [".NETFramework,Version=v4.8"] = new List<string> { "PackageA" }
        },
        PackagesByTarget = new Dictionary<string, IReadOnlyDictionary<string, PackageInfo>>
        {
            ["net8.0"] = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>()),
                ["PackageB"] = new("PackageB", "2.0.0", new Dictionary<string, string> { ["PackageA"] = "1.0.0" })
            },
            [".NETFramework,Version=v4.8"] = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>())
            }
        },
        AvailableTargets = ["net8.0", ".NETFramework,Version=v4.8"]
    };

    public DependencyGraphBuilderTests()
    {
        _builder = new DependencyGraphBuilder(_parser, _resolver);
        _parser.Parse(Arg.Any<string>()).Returns(SampleContent);
    }

    [Fact]
    public void Build_DefaultFramework_SelectsFirst()
    {
        _resolver.ResolveTargetsKey("net8.0", Arg.Any<IReadOnlyList<string>>()).Returns("net8.0");

        var graph = _builder.Build("TestProject", "{}", null);

        Assert.Equal("TestProject", graph.ProjectName);
        Assert.Equal("net8.0", graph.TargetFramework);
    }

    [Fact]
    public void Build_DirectDeps_AreSortedAlphabetically()
    {
        _resolver.ResolveTargetsKey("net8.0", Arg.Any<IReadOnlyList<string>>()).Returns("net8.0");

        var graph = _builder.Build("TestProject", "{}", null);

        Assert.Equal(new[] { "PackageA", "PackageB" }, graph.DirectDependencies);
    }

    [Fact]
    public void Build_WithShortAlias_ResolvesToLongFormFramework()
    {
        _resolver.ResolveTargetsKey(".NETFramework,Version=v4.8", Arg.Any<IReadOnlyList<string>>())
            .Returns(".NETFramework,Version=v4.8");

        var graph = _builder.Build("TestProject", "{}", "net48");

        Assert.Equal(".NETFramework,Version=v4.8", graph.TargetFramework);
        Assert.Single(graph.DirectDependencies);
    }

    [Fact]
    public void Build_UnknownFramework_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _builder.Build("TestProject", "{}", "net99"));
    }

    [Fact]
    public void Build_NoFrameworks_ThrowsInvalidOperationException()
    {
        var emptyContent = new AssetsFileContent
        {
            AvailableFrameworks = new List<string>(),
            DirectDependenciesByFramework = new Dictionary<string, IReadOnlyList<string>>(),
            PackagesByTarget = new Dictionary<string, IReadOnlyDictionary<string, PackageInfo>>(),
            AvailableTargets = new List<string>()
        };
        _parser.Parse(Arg.Any<string>()).Returns(emptyContent);

        Assert.Throws<InvalidOperationException>(() =>
            _builder.Build("TestProject", "{}", null));
    }

    [Fact]
    public void Build_TargetsKeyNotResolved_ThrowsInvalidOperationException()
    {
        _resolver.ResolveTargetsKey(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns((string?)null);

        Assert.Throws<InvalidOperationException>(() =>
            _builder.Build("TestProject", "{}", null));
    }

    [Fact]
    public void Build_IncludesAvailableFrameworks()
    {
        _resolver.ResolveTargetsKey("net8.0", Arg.Any<IReadOnlyList<string>>()).Returns("net8.0");

        var graph = _builder.Build("TestProject", "{}", null);

        Assert.Equal(2, graph.AvailableFrameworks.Count);
        Assert.Contains("net8.0", graph.AvailableFrameworks);
        Assert.Contains(".NETFramework,Version=v4.8", graph.AvailableFrameworks);
    }

    [Fact]
    public void Build_WithLongFormFramework_SelectsExactMatch()
    {
        _resolver.ResolveTargetsKey(".NETFramework,Version=v4.8", Arg.Any<IReadOnlyList<string>>())
            .Returns(".NETFramework,Version=v4.8");

        var graph = _builder.Build("TestProject", "{}", ".NETFramework,Version=v4.8");

        Assert.Equal(".NETFramework,Version=v4.8", graph.TargetFramework);
    }

    [Theory]
    [InlineData("netstandard2.0")]
    [InlineData("netcoreapp3.1")]
    [InlineData("net48")]
    public void Build_WithUnknownShortAlias_ThrowsWhenNoMatchingFramework(string alias)
    {
        var singleFrameworkContent = new AssetsFileContent
        {
            AvailableFrameworks = ["net8.0"],
            DirectDependenciesByFramework = new Dictionary<string, IReadOnlyList<string>>
            {
                ["net8.0"] = new List<string> { "PackageA" }
            },
            PackagesByTarget = new Dictionary<string, IReadOnlyDictionary<string, PackageInfo>>
            {
                ["net8.0"] = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PackageA"] = new("PackageA", "1.0.0", new Dictionary<string, string>())
                }
            },
            AvailableTargets = ["net8.0"]
        };
        _parser.Parse(Arg.Any<string>()).Returns(singleFrameworkContent);

        Assert.Throws<ArgumentException>(() =>
            _builder.Build("TestProject", "{}", alias));
    }
}
