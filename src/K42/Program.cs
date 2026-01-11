using System.CommandLine;
using K42.Commands;
using K42.Logging;

namespace K42;

/// <summary>
/// K42 - A single-node container execution system.
/// Boring on purpose. Reliable. Humane.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("K42 - Run containers. Keep them running. Understand what they do.")
        {
            RunCommand.Create(),
            StatusCommand.Create(),
            StopCommand.Create(),
            ListCommand.Create(),
            LogsCommand.Create(),
            UnregisterCommand.Create(),
            VersionCommand.Create()
        };

        rootCommand.Description = @"
K42 is a containerized executable for YAML files.

One file = one container. No orchestration. No complexity.

Commands:
  run <file>        Execute a K42 YAML script
  status <name>     Show container status
  stop <name>       Stop a running container
  list              List all K42 containers
  logs <name>       View container logs
  unregister <name> Remove container registration
  version           Show version and build info

Examples:
  k42 run ./my-service.k42
  k42 status my-service
  k42 list
";

        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            SystemLogger.Error($"Fatal error: {ex.Message}");
            return 1;
        }
    }
}
