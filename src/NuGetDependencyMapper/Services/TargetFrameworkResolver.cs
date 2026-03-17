namespace NuGetDependencyMapper.Services;

public class TargetFrameworkResolver : ITargetFrameworkResolver
{
    public string? ResolveTargetsKey(string frameworkMoniker, IReadOnlyList<string> availableTargets)
    {
        // 1. Exact match
        var match = availableTargets.FirstOrDefault(
            k => k.Equals(frameworkMoniker, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 2. Starts-with match (e.g., "net8.0" matches "net8.0-windows")
        match = availableTargets.FirstOrDefault(
            k => k.StartsWith(frameworkMoniker, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 3. Normalize short alias to long-form moniker and retry
        var longForm = ConvertToLongFormMoniker(frameworkMoniker);
        if (longForm != null)
        {
            match = availableTargets.FirstOrDefault(
                k => k.Equals(longForm, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            match = availableTargets.FirstOrDefault(
                k => k.StartsWith(longForm, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 4. Reverse: the moniker might be long-form, try matching against shorter targets
        match = availableTargets.FirstOrDefault(
            k => frameworkMoniker.StartsWith(k, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // 5. If only one target exists, use it as a fallback
        if (availableTargets.Count == 1)
            return availableTargets[0];

        return null;
    }

    public static string? ConvertToLongFormMoniker(string shortMoniker)
    {
        // .NET Framework: net48 -> .NETFramework,Version=v4.8
        if (shortMoniker.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
            !shortMoniker.Contains('.') &&
            shortMoniker.Length >= 5 &&
            char.IsDigit(shortMoniker[3]))
        {
            var versionDigits = shortMoniker[3..];

            if (versionDigits.Length >= 2 && versionDigits[0] is >= '1' and <= '4')
            {
                var major = versionDigits[0];
                var minor = versionDigits[1];
                var version = $"{major}.{minor}";

                if (versionDigits.Length > 2)
                    version += $".{versionDigits[2..]}";

                return $".NETFramework,Version=v{version}";
            }
        }

        // .NET Standard: netstandard2.0 -> .NETStandard,Version=v2.0
        if (shortMoniker.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            var version = shortMoniker["netstandard".Length..];
            return $".NETStandard,Version=v{version}";
        }

        // .NET Core: netcoreapp3.1 -> .NETCoreApp,Version=v3.1
        if (shortMoniker.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            var version = shortMoniker["netcoreapp".Length..];
            return $".NETCoreApp,Version=v{version}";
        }

        return null;
    }
}
