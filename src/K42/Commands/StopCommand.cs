using System.CommandLine;
using K42.Logging;
using K42.Runtime;

namespace K42.Commands;

/// <summary>
/// k42 stop <name>
/// 
/// Stop a running container gracefully.
/// </summary>
public static class StopCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>(
            name: "name",
            description: "The container name to stop");

        var command = new Command("stop", "Stop a running container")
        {
            nameArg
        };

        command.SetHandler(async (string name) =>
        {
            await Execute(name);
        }, nameArg);

        return command;
    }

    private static async Task Execute(string name)
    {
        using var manager = new ContainerManager();

        if (!await manager.IsRuntimeAvailable())
        {
            SystemLogger.Error("Docker is not running or not accessible");
            Environment.ExitCode = 1;
            return;
        }

        var state = await manager.GetStatus(name);

        if (state.Status == ContainerStatus.NotFound)
        {
            SystemLogger.Error($"Container not found: {name}");
            Environment.ExitCode = 1;
            return;
        }

        if (state.Status == ContainerStatus.Stopped)
        {
            Console.WriteLine($"Container '{name}' is already stopped");
            return;
        }

        Console.WriteLine($"Stopping container '{name}'...");

        var success = await manager.Stop(name);

        if (success)
        {
            Console.WriteLine($"✓ Container '{name}' stopped");
        }
        else
        {
            SystemLogger.Error($"Failed to stop container '{name}'");
            Environment.ExitCode = 1;
        }
    }
}
