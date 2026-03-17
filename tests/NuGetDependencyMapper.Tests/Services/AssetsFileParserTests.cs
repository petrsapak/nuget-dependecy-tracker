using NuGetDependencyMapper.Services;

namespace NuGetDependencyMapper.Tests.Services;

public class AssetsFileParserTests
{
    private readonly AssetsFileParser _parser = new();
    private readonly string _testAssetsJson;

    public AssetsFileParserTests()
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData", "minimal-assets.json");
        _testAssetsJson = File.ReadAllText(testDataPath);
    }

    [Fact]
    public void Parse_ExtractsAvailableFrameworks()
    {
        var result = _parser.Parse(_testAssetsJson);
        Assert.Single(result.AvailableFrameworks);
        Assert.Equal(".NETFramework,Version=v4.8", result.AvailableFrameworks[0]);
    }

    [Fact]
    public void Parse_ExtractsDirectDependencies_ExcludingProjectReferences()
    {
        var result = _parser.Parse(_testAssetsJson);
        var directDeps = result.DirectDependenciesByFramework[".NETFramework,Version=v4.8"];

        Assert.Contains("Logging.Core", directDeps);
        Assert.Contains("Newtonsoft.Json", directDeps);
        Assert.Contains("RestSharp", directDeps);
        Assert.Contains("DataAccess", directDeps);
        Assert.DoesNotContain("MySharedLib", directDeps);
    }

    [Fact]
    public void Parse_ExtractsAvailableTargets()
    {
        var result = _parser.Parse(_testAssetsJson);
        Assert.Single(result.AvailableTargets);
        Assert.Equal(".NETFramework,Version=v4.8", result.AvailableTargets[0]);
    }

    [Fact]
    public void Parse_ExtractsPackagesWithVersions()
    {
        var result = _parser.Parse(_testAssetsJson);
        var packages = result.PackagesByTarget[".NETFramework,Version=v4.8"];

        Assert.True(packages.ContainsKey("log4net"));
        Assert.Equal("2.0.10", packages["log4net"].Version);

        Assert.True(packages.ContainsKey("RestSharp"));
        Assert.Equal("112.0.0", packages["RestSharp"].Version);
    }

    [Fact]
    public void Parse_ExtractsTransitiveDependencies()
    {
        var result = _parser.Parse(_testAssetsJson);
        var packages = result.PackagesByTarget[".NETFramework,Version=v4.8"];

        var restSharp = packages["RestSharp"];
        Assert.Single(restSharp.Dependencies);
        Assert.True(restSharp.Dependencies.ContainsKey("System.Text.Json"));
    }

    [Fact]
    public void Parse_SkipsProjectReferencesInTargets()
    {
        var result = _parser.Parse(_testAssetsJson);
        var packages = result.PackagesByTarget[".NETFramework,Version=v4.8"];

        Assert.False(packages.ContainsKey("MySharedLib"));
    }

    [Fact]
    public void Parse_PackageWithNoDependencies_HasEmptyDictionary()
    {
        var result = _parser.Parse(_testAssetsJson);
        var packages = result.PackagesByTarget[".NETFramework,Version=v4.8"];

        Assert.Empty(packages["Newtonsoft.Json"].Dependencies);
    }
}
