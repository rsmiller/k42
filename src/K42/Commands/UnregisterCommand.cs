using System.CommandLine;
using K42.Logging;
using K42.Runtime;

namespace K42.Commands;

/// <summary>
/// k42 unregister <name>
/// 
/// Stop and remove a container, including its registration and storage.
/// </summary>
public static class UnregisterCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>(
            name: "name",
            description: "The container name to unregister");

        var forceOption = new Option<bool>(
            name: "--force",
            getDefaultValue: () => false,
            description: "Skip confirmation prompt");
        forceOption.AddAlias("-f");

        var command = new Command("unregister", "Remove a container and its data")
        {
            nameArg,
            forceOption
        };

        command.SetHandler(async (string name, bool force) =>
        {
            await Execute(name, force);
        }, nameArg, forceOption);

        return command;
    }

    private static async Task Execute(string name, bool force)
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
            // Check if there's a registration without a container
            var registration = manager.GetRegistration(name);
            if (registration == null)
            {
                SystemLogger.Error($"Container not found: {name}");
                Environment.ExitCode = 1;
                return;
            }
        }

        if (!force)
        {
            Console.WriteLine($"This will permanently remove:");
            Console.WriteLine($"  - Container: k42-{name}");
            Console.WriteLine($"  - Volume: k42-{name}-data");
            Console.WriteLine($"  - Registration for '{name}'");
            Console.WriteLine();
            Console.Write("Are you sure? [y/N]: ");

            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Cancelled.");
                return;
            }
        }

        Console.WriteLine($"Removing container '{name}'...");

        var success = await manager.Unregister(name);

        if (success)
        {
            Console.WriteLine($"✓ Container '{name}' removed");
        }
        else
        {
            // Might have been already removed, check registration
            var registration = manager.GetRegistration(name);
            if (registration != null)
            {
                Console.WriteLine($"Container not found, but registration removed.");
            }
            else
            {
                SystemLogger.Error($"Failed to remove container '{name}'");
                Environment.ExitCode = 1;
            }
        }
    }
}
