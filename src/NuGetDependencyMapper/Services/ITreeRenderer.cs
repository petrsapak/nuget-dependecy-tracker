using NuGetDependencyMapper.Models;
using Spectre.Console;

namespace NuGetDependencyMapper.Services;

public interface ITreeRenderer
{
    /// <summary>
    /// Renders the full dependency tree using Spectre.Console tree widget.
    /// </summary>
    void RenderFullTree(DependencyGraph graph, IAnsiConsole console);

    /// <summary>
    /// Renders the trace result showing all paths to a specific package.
    /// </summary>
    void RenderTrace(DependencyGraph graph, TraceResult traceResult, IAnsiConsole console);
}
