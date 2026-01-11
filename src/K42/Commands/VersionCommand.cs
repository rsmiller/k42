using System.CommandLine;
using System.Reflection;

namespace K42.Commands;

/// <summary>
/// k42 version
/// 
/// Show version and build information.
/// Useful for debugging to confirm which build is running.
/// </summary>
public static class VersionCommand
{
    public static Command Create()
    {
        var command = new Command("version", "Show version and build information");

        command.SetHandler(() =>
        {
            Execute();
        });

        return command;
    }

    private static void Execute()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? version;
        
        // Get build timestamp from assembly
        var buildDate = GetBuildDate(assembly);

        Console.WriteLine($"k42 version {informationalVersion}");
        Console.WriteLine($"  Build date: {buildDate:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"  Runtime: {Environment.Version}");
        Console.WriteLine($"  OS: {Environment.OSVersion}");
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        // Use the assembly's last write time as build date
        var location = assembly.Location;
        if (!string.IsNullOrEmpty(location) && File.Exists(location))
        {
            return File.GetLastWriteTimeUtc(location);
        }
        
        // Fallback for single-file deployments
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath) && File.Exists(processPath))
        {
            return File.GetLastWriteTimeUtc(processPath);
        }

        return DateTime.MinValue;
    }
}
