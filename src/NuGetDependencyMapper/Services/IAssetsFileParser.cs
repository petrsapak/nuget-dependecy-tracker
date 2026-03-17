using NuGetDependencyMapper.Models;

namespace NuGetDependencyMapper.Services;

public interface IAssetsFileParser
{
    /// <summary>
    /// Parses the contents of a project.assets.json file into a structured representation.
    /// </summary>
    AssetsFileContent Parse(string assetsJsonContent);
}
