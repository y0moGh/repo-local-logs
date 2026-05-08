namespace EvWeb.LogMonitor.Domain.Constants;

public static class LogFolders
{
    public const string Errors = "errors";
    public const string Info = "info";
    public const string Warnings = "warnings";
    public const string Legacy = "legacy"; 

    public static readonly IReadOnlyList<string> All = new[]
    {
        Errors,
        Info,
        Warnings,
        Legacy
    };

    public static bool IsValid(string? folder)
    {
        return All.Contains(folder ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    public static string Normalize(string? folder)
    {
        var match = All.FirstOrDefault(item => item.Equals(folder, StringComparison.OrdinalIgnoreCase));
        return match ?? Info;
    }

    public static string ToDisplayName(string folder)
    {
        return Normalize(folder) switch
        {
            Errors => "Errors",
            Warnings => "Warnings",
            Legacy => "Legacy",
            _ => "Info"

        };
    }

    public static bool UsesBasePath(string? folder)
    {
        return folder?.Equals(Legacy, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
