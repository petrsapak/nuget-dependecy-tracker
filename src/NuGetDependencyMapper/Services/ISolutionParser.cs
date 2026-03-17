namespace NuGetDependencyMapper.Services;

public interface ISolutionParser
{
    /// <summary>
    /// Extracts all .csproj file paths from a .sln file.
    /// Returns absolute paths resolved relative to the solution directory.
    /// </summary>
    IReadOnlyList<string> GetProjectPaths(string solutionFilePath, string solutionContent);
}
