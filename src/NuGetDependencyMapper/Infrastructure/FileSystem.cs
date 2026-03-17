using System.Text;

namespace NuGetDependencyMapper.Infrastructure;

public class FileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public TextWriter CreateFileWriter(string path) =>
        new StreamWriter(path, append: false, encoding: Encoding.UTF8);
}
