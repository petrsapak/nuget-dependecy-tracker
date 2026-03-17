using NuGetDependencyMapper.Cli;
using Spectre.Console.Cli;

var app = new CommandApp<MapCommand>();

app.Configure(config =>
{
    config.SetApplicationName("nuget-dependency-mapper");
    config.SetApplicationVersion(
        typeof(MapCommand).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    config.AddExample(["MyProject.csproj"]);
    config.AddExample(["MySolution.sln"]);
    config.AddExample(["MyProject.csproj", "-f", "net48"]);
    config.AddExample(["MyProject.csproj", "-t", "log4net"]);
    config.AddExample(["MySolution.sln", "-t", "Newtonsoft.Json"]);
    config.AddExample(["MySolution.sln", "-o", "dependencies.txt"]);
    config.AddExample(["MySolution.sln", "-f", "net48", "-t", "Microsoft.Data.SqlClient", "-o", "trace.txt"]);
});

return app.Run(args);
