using NuGetDependencyMapper.Services;

namespace NuGetDependencyMapper.Tests.Services;

public class TargetFrameworkResolverTests
{
    private readonly TargetFrameworkResolver _resolver = new();

    [Fact]
    public void ResolveTargetsKey_ExactMatch_ReturnsMatch()
    {
        var targets = new List<string> { "net8.0", "net6.0" };
        Assert.Equal("net8.0", _resolver.ResolveTargetsKey("net8.0", targets));
    }

    [Fact]
    public void ResolveTargetsKey_StartsWithMatch_HandlesRidSuffix()
    {
        var targets = new List<string> { "net8.0-windows7.0" };
        Assert.Equal("net8.0-windows7.0", _resolver.ResolveTargetsKey("net8.0", targets));
    }

    [Fact]
    public void ResolveTargetsKey_Net48ShortAlias_ResolvesToLongForm()
    {
        var targets = new List<string> { ".NETFramework,Version=v4.8" };
        Assert.Equal(".NETFramework,Version=v4.8", _resolver.ResolveTargetsKey("net48", targets));
    }

    [Fact]
    public void ResolveTargetsKey_Net472ShortAlias_ResolvesToLongForm()
    {
        var targets = new List<string> { ".NETFramework,Version=v4.7.2" };
        Assert.Equal(".NETFramework,Version=v4.7.2", _resolver.ResolveTargetsKey("net472", targets));
    }

    [Fact]
    public void ResolveTargetsKey_Net461ShortAlias_ResolvesToLongForm()
    {
        var targets = new List<string> { ".NETFramework,Version=v4.6.1" };
        Assert.Equal(".NETFramework,Version=v4.6.1", _resolver.ResolveTargetsKey("net461", targets));
    }

    [Fact]
    public void ResolveTargetsKey_NetStandard_ResolvesToLongForm()
    {
        var targets = new List<string> { ".NETStandard,Version=v2.0" };
        Assert.Equal(".NETStandard,Version=v2.0", _resolver.ResolveTargetsKey("netstandard2.0", targets));
    }

    [Fact]
    public void ResolveTargetsKey_NetCoreApp_ResolvesToLongForm()
    {
        var targets = new List<string> { ".NETCoreApp,Version=v3.1" };
        Assert.Equal(".NETCoreApp,Version=v3.1", _resolver.ResolveTargetsKey("netcoreapp3.1", targets));
    }

    [Fact]
    public void ResolveTargetsKey_SingleTargetFallback_ReturnsThatTarget()
    {
        var targets = new List<string> { "some-unusual-target" };
        Assert.Equal("some-unusual-target", _resolver.ResolveTargetsKey("anything", targets));
    }

    [Fact]
    public void ResolveTargetsKey_NoMatch_ReturnsNull()
    {
        var targets = new List<string> { "net6.0", "net7.0" };
        Assert.Null(_resolver.ResolveTargetsKey("net48", targets));
    }

    [Fact]
    public void ResolveTargetsKey_CaseInsensitive_Matches()
    {
        var targets = new List<string> { "Net8.0" };
        Assert.Equal("Net8.0", _resolver.ResolveTargetsKey("NET8.0", targets));
    }

    [Theory]
    [InlineData("net48", ".NETFramework,Version=v4.8")]
    [InlineData("net472", ".NETFramework,Version=v4.7.2")]
    [InlineData("net461", ".NETFramework,Version=v4.6.1")]
    [InlineData("net20", ".NETFramework,Version=v2.0")]
    [InlineData("netstandard2.0", ".NETStandard,Version=v2.0")]
    [InlineData("netstandard2.1", ".NETStandard,Version=v2.1")]
    [InlineData("netcoreapp3.1", ".NETCoreApp,Version=v3.1")]
    public void ConvertToLongFormMoniker_CorrectConversions(string shortForm, string expectedLongForm)
    {
        Assert.Equal(expectedLongForm, TargetFrameworkResolver.ConvertToLongFormMoniker(shortForm));
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net6.0")]
    [InlineData("something-random")]
    public void ConvertToLongFormMoniker_ModernOrInvalid_ReturnsNull(string moniker)
    {
        Assert.Null(TargetFrameworkResolver.ConvertToLongFormMoniker(moniker));
    }
}
