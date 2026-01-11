using System.CommandLine;
using K42.Logging;
using K42.Runtime;

namespace K42.Commands;

/// <summary>
/// k42 logs <name>
/// 
/// View container logs. Human-readable. Simple.
/// </summary>
public static class LogsCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>(
            name: "name",
            description: "The container name");

        var tailOption = new Option<int>(
            name: "--tail",
            getDefaultValue: () => 100,
            description: "Number of lines to show from the end");
        tailOption.AddAlias("-n");

        var followOption = new Option<bool>(
            name: "--follow",
            getDefaultValue: () => false,
            description: "Follow log output (not implemented - use docker logs -f)");
        followOption.AddAlias("-f");

        var command = new Command("logs", "View container logs")
        {
            nameArg,
            tailOption,
            followOption
        };

        command.SetHandler(async (string name, int tail, bool follow) =>
        {
            await Execute(name, tail, follow);
        }, nameArg, tailOption, followOption);

        return command;
    }

    private static async Task Execute(string name, int tailLines, bool follow)
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

        if (follow)
        {
            Console.WriteLine("Note: --follow is not implemented in K42.");
            Console.WriteLine($"Use: docker logs -f k42-{name}");
            Console.WriteLine();
        }

        var logs = await manager.GetLogs(name, tailLines);

        if (string.IsNullOrWhiteSpace(logs))
        {
            Console.WriteLine("(no logs)");
        }
        else
        {
            Console.WriteLine(logs);
        }
    }
}
