using System.CommandLine;
using K42.Logging;
using K42.Runtime;

namespace K42.Commands;

/// <summary>
/// k42 list
/// 
/// List all K42 containers with human-readable status.
/// </summary>
public static class ListCommand
{
    public static Command Create()
    {
        var command = new Command("list", "List all K42 containers");

        command.SetHandler(async () =>
        {
            await Execute();
        });

        return command;
    }

    private static async Task Execute()
    {
        using var manager = new ContainerManager();

        if (!await manager.IsRuntimeAvailable())
        {
            SystemLogger.Error("Docker is not running or not accessible");
            Environment.ExitCode = 1;
            return;
        }

        var containers = await manager.GetAllContainers();

        if (containers.Count == 0)
        {
            Console.WriteLine("No K42 containers found.");
            Console.WriteLine();
            Console.WriteLine("Run a container with: k42 run <script.k42>");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{"NAME",-20} {"STATUS",-12} {"IMAGE",-30} {"PORT",-8} {"UPTIME",-15}");
        Console.WriteLine(new string('─', 90));

        foreach (var container in containers.OrderBy(c => c.Name))
        {
            var status = container.Status switch
            {
                ContainerStatus.Running => "Running",
                ContainerStatus.Stopped => "Stopped",
                ContainerStatus.Failed => "Failed",
                ContainerStatus.Creating => "Creating",
                _ => "Unknown"
            };

            var port = container.HostPort > 0 ? container.HostPort.ToString() : "-";
            
            var uptime = "-";
            if (container.Status == ContainerStatus.Running && container.StartedAt.HasValue)
            {
                var duration = DateTime.UtcNow - container.StartedAt.Value;
                uptime = FormatUptime(duration);
            }

            // Truncate image name if too long
            var image = container.Image;
            if (image.Length > 28)
            {
                image = image[..25] + "...";
            }

            // Color based on status
            switch (container.Status)
            {
                case ContainerStatus.Running:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case ContainerStatus.Failed:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case ContainerStatus.Stopped:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
            }

            Console.WriteLine($"{container.Name,-20} {status,-12} {image,-30} {port,-8} {uptime,-15}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {containers.Count} container(s)");
        Console.WriteLine($"  Running: {containers.Count(c => c.Status == ContainerStatus.Running)}");
        Console.WriteLine($"  Stopped: {containers.Count(c => c.Status == ContainerStatus.Stopped)}");
        Console.WriteLine($"  Failed: {containers.Count(c => c.Status == ContainerStatus.Failed)}");
        Console.WriteLine();
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        if (uptime.TotalMinutes >= 1)
            return $"{(int)uptime.TotalMinutes}m";
        return $"{uptime.Seconds}s";
    }
}
