# NuGet Dependency Mapper

A CLI tool that visualizes the NuGet dependency tree for .NET projects and solutions, showing both direct and transitive dependencies. It can also trace all dependency paths leading to a specific package — useful for understanding why a transitive dependency exists.

Built with [Spectre.Console](https://spectreconsole.net/) for rich, color-coded terminal output.

## Prerequisites

- .NET 8.0 SDK or later
- `dotnet restore` must have been run on the target project(s) (the tool reads `obj/project.assets.json`)

## Build

```bash
dotnet build
```

## Usage

```bash
# Show the full dependency tree for a single project
dotnet run --project src/NuGetDependencyMapper -- <path-to.csproj>

# Show dependency trees for all projects in a solution
dotnet run --project src/NuGetDependencyMapper -- <path-to.sln>

# Select a specific target framework (for multi-target projects)
dotnet run --project src/NuGetDependencyMapper -- <path-to.csproj> -f net48

# Trace all paths to a specific package
dotnet run --project src/NuGetDependencyMapper -- <path-to.csproj> -t log4net

# Trace a package across an entire solution
dotnet run --project src/NuGetDependencyMapper -- <path-to.sln> -t log4net

# Export the tree to a file (plain text, no colors)
dotnet run --project src/NuGetDependencyMapper -- <path-to.sln> -o dependencies.txt

# Combine options
dotnet run --project src/NuGetDependencyMapper -- <path-to.sln> -f net48 -t Microsoft.Data.SqlClient -o trace.txt
```

### Options

| Option | Short | Description |
|--------|-------|-------------|
| `--framework` | `-f` | Select a specific target framework |
| `--output` | `-o` | Export the tree to a file (plain text, no ANSI colors) |
| `--trace` | `-t` | Trace all dependency paths to a specific package |
| `--help` | `-h` | Show help message |

### Help Output

Running with `--help` displays auto-generated, color-coded usage information:

```
USAGE:
    nuget-dependency-mapper <PATH> [OPTIONS]

ARGUMENTS:
    <PATH>    Path to a .csproj or .sln file

OPTIONS:
    -h, --help               Prints help information
    -f, --framework <TFM>    Select a specific target framework
    -o, --output <FILE>      Export the tree to a file
    -t, --trace <PACKAGE>    Trace all dependency paths to a specific package
```

## Example Output

> In the terminal, package names appear in **green**, versions in **dim gray**, project headers in **bold blue**, and errors in **red**. The examples below show plain-text equivalents.

### Single Project

```
MyProject (.NETFramework,Version=v4.8)
├── DataAccess 2.0.0
│   ├── log4net 2.0.10
│   └── System.Text.Json 8.0.0
│       └── System.Memory 4.5.5
├── Logging.Core 3.2.0
│   └── log4net 2.0.10
├── Newtonsoft.Json 13.0.3
└── RestSharp 112.0.0
    └── System.Text.Json 8.0.0
        └── System.Memory 4.5.5
```

### Solution Mode

Projects are separated by horizontal rules, with a summary footer:

```
Solution: MySolution.sln — 3 project(s) found

MyApp (.NETFramework,Version=v4.8)
├── DataAccess 2.0.0
│   └── log4net 2.0.10
├── Newtonsoft.Json 13.0.3
└── RestSharp 112.0.0
    └── System.Text.Json 8.0.0

────────────────────────────────────────────────────────────────────────────────

MyApp.Core (.NETFramework,Version=v4.8)
├── Newtonsoft.Json 13.0.3
└── System.Text.Json 8.0.0

────────────────────────────────────────────────────────────────────────────────

MyApp.Tests (.NETFramework,Version=v4.8)
├── Moq 4.20.0
│   └── Castle.Core 5.1.0
└── xunit 2.6.0

──────────────── 3 project(s) processed, 0 skipped ────────────────
```

### Trace Mode

Trace results are displayed with a bordered panel header and numbered tree paths:

```
╭──────────────────────────────────────────────────────────╮
│ Tracing paths to: log4net (2.0.10)                       │
│ Project: MyProject (.NETFramework,Version=v4.8)          │
╰──────────────────────────────────────────────────────────╯

Path 1:
└── DataAccess 2.0.0
    └── log4net 2.0.10

Path 2:
└── Logging.Core 3.2.0
    └── log4net 2.0.10

Total: 2 path(s) found.
```

### Error Messages

Errors and warnings use color-coded prefixes:

```
Error: File not found: /path/to/missing.csproj
Error: Input must be a .csproj or .sln file.
Skipping MyLib: project.assets.json not found (run dotnet restore)
```

### File Export

When using `--output`, the tree is written as plain text with no ANSI escape codes — suitable for sharing, diffing, or piping to other tools:

```bash
dotnet run --project src/NuGetDependencyMapper -- MySolution.sln -o deps.txt
# => Dependency tree exported to: /full/path/to/deps.txt
```

## Running Tests

```bash
dotnet test
```

## Target Framework Support

The tool handles all common .NET target framework formats:

| Short Alias | Resolved To |
|-------------|-------------|
| `net48` | `.NETFramework,Version=v4.8` |
| `net472` | `.NETFramework,Version=v4.7.2` |
| `net461` | `.NETFramework,Version=v4.6.1` |
| `netstandard2.0` | `.NETStandard,Version=v2.0` |
| `netcoreapp3.1` | `.NETCoreApp,Version=v3.1` |
| `net8.0` | Matched directly or with RID suffix |

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Extensions.DependencyInjection` | Service registration and resolution |
| `Spectre.Console.Cli` | CLI argument parsing, help generation, and rich terminal rendering |
