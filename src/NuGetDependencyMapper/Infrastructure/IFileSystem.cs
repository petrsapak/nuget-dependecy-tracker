namespace NuGetDependencyMapper.Infrastructure;

public interface IFileSystem
{
    bool FileExists(string path);
    string ReadAllText(string path);
    TextWriter CreateFileWriter(string path);
}
