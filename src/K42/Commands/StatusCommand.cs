using System.CommandLine;
using K42.Logging;
using K42.Runtime;

namespace K42.Commands;

/// <summary>
/// k42 status <name>
/// 
/// Show human-readable status of a container.
/// Answers: Is it running? What image? What port? When did it last restart?
/// </summary>
public static class StatusCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>(
            name: "name",
            description: "The container name");

        var command = new Command("status", "Show container status")
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
        var registration = manager.GetRegistration(name);

        Console.WriteLine();
        Console.WriteLine($"Container: {name}");
        Console.WriteLine(new string('─', 40));

        if (state.Status == ContainerStatus.NotFound)
        {
            Console.WriteLine("Status: NOT FOUND");
            Console.WriteLine();
            Console.WriteLine("This container does not exist.");
            Console.WriteLine("Run 'k42 list' to see all containers.");
            return;
        }

        // Status with color
        Console.Write("Status: ");
        switch (state.Status)
        {
            case ContainerStatus.Running:
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("RUNNING ✓");
                break;
            case ContainerStatus.Stopped:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("STOPPED");
                break;
            case ContainerStatus.Failed:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED ✗");
                break;
            default:
                Console.WriteLine(state.Status.ToString().ToUpper());
                break;
        }
        Console.ResetColor();

        Console.WriteLine($"Image: {state.Image}");
        Console.WriteLine($"Container ID: {state.ContainerId}");

        if (state.HostPort > 0)
        {
            Console.WriteLine($"Port: {state.HostPort}");
        }

        if (state.StartedAt.HasValue)
        {
            var uptime = DateTime.UtcNow - state.StartedAt.Value;
            Console.WriteLine($"Started: {state.StartedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Uptime: {FormatUptime(uptime)}");
        }

        if (state.RestartCount > 0)
        {
            Console.WriteLine($"Restarts: {state.RestartCount}");
        }

        if (state.LastRestartAt.HasValue)
        {
            Console.WriteLine($"Last Restart: {state.LastRestartAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
        }

        if (!string.IsNullOrEmpty(state.ExitReason))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Exit Reason: {state.ExitReason}");
            Console.ResetColor();
        }

        if (registration != null)
        {
            Console.WriteLine();
            Console.WriteLine("Registration:");
            Console.WriteLine($"  Script: {registration.ScriptPath}");
            Console.WriteLine($"  Registered: {registration.RegisteredAt:yyyy-MM-dd HH:mm:ss} UTC");
        }

        Console.WriteLine();
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        if (uptime.TotalMinutes >= 1)
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        return $"{uptime.Seconds}s";
    }
}
