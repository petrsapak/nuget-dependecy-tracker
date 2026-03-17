using NuGetDependencyMapper.Services;

namespace NuGetDependencyMapper.Tests.Services;

public class SolutionParserTests
{
    private readonly SolutionParser _parser = new();

    private const string SampleSolution = """

        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        VisualStudioVersion = 17.0.31903.59
        MinimumVisualStudioVersion = 10.0.40219.1
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyApp", "src\MyApp\MyApp.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyApp.Core", "src\MyApp.Core\MyApp.Core.csproj", "{B2C3D4E5-F6A7-8901-BCDE-F12345678901}"
        EndProject
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyApp.Tests", "tests\MyApp.Tests\MyApp.Tests.csproj", "{C3D4E5F6-A7B8-9012-CDEF-123456789012}"
        EndProject
        Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Solution Items", "Solution Items", "{D4E5F6A7-B8C9-0123-DEFA-234567890123}"
        EndProject
        Global
        EndGlobal
        """;

    [Fact]
    public void GetProjectPaths_ExtractsCsprojPaths()
    {
        var paths = _parser.GetProjectPaths(@"C:\repo\MySolution.sln", SampleSolution);

        Assert.Equal(3, paths.Count);
    }

    [Fact]
    public void GetProjectPaths_ResolvesRelativeToSolutionDirectory()
    {
        var paths = _parser.GetProjectPaths(@"C:\repo\MySolution.sln", SampleSolution);

        Assert.Contains(paths, p => p.EndsWith("MyApp.csproj"));
        Assert.Contains(paths, p => p.EndsWith("MyApp.Core.csproj"));
        Assert.Contains(paths, p => p.EndsWith("MyApp.Tests.csproj"));
    }

    [Fact]
    public void GetProjectPaths_SortedAlphabetically()
    {
        var paths = _parser.GetProjectPaths(@"C:\repo\MySolution.sln", SampleSolution);
        var fileNames = paths.Select(Path.GetFileName).ToList();

        Assert.Equal(fileNames.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList(), fileNames);
    }

    [Fact]
    public void GetProjectPaths_ExcludesSolutionFolders()
    {
        // Solution folder GUIDs (2150E333-...) should not be matched
        var paths = _parser.GetProjectPaths(@"C:\repo\MySolution.sln", SampleSolution);

        Assert.DoesNotContain(paths, p => p.Contains("Solution Items"));
    }

    [Fact]
    public void GetProjectPaths_EmptySolution_ReturnsEmptyList()
    {
        const string emptySln = """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Global
            EndGlobal
            """;

        var paths = _parser.GetProjectPaths(@"C:\repo\Empty.sln", emptySln);

        Assert.Empty(paths);
    }

    [Fact]
    public void GetProjectPaths_IgnoresNonCsprojProjects()
    {
        const string mixedSln = """
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyApp", "src\MyApp\MyApp.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
            EndProject
            Project("{F2A71F9B-5D33-465A-A702-920D77279786}") = "MyFSharp", "src\MyFSharp\MyFSharp.fsproj", "{B2C3D4E5-F6A7-8901-BCDE-F12345678901}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyVb", "src\MyVb\MyVb.vbproj", "{C3D4E5F6-A7B8-9012-CDEF-123456789012}"
            EndProject
            """;

        var paths = _parser.GetProjectPaths(@"C:\repo\Mixed.sln", mixedSln);

        // Only .csproj should be matched
        Assert.Single(paths);
        Assert.Contains(paths, p => p.EndsWith("MyApp.csproj"));
    }
}
