using System.CommandLine;
using K42.Logging;
using K42.Runtime;
using K42.Schema;

namespace K42.Commands;

/// <summary>
/// k42 run <file>
/// 
/// Execute a K42 YAML script file.
/// If container exists → do nothing.
/// If new → create from YAML.
/// </summary>
public static class RunCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<FileInfo>(
            name: "file",
            description: "The K42 script file to execute");

        var command = new Command("run", "Execute a K42 script file")
        {
            fileArg
        };

        command.SetHandler(async (FileInfo file) =>
        {
            await Execute(file);
        }, fileArg);

        return command;
    }

    private static async Task Execute(FileInfo file)
    {
        if (!file.Exists)
        {
            SystemLogger.Error($"File not found: {file.FullName}");
            Environment.ExitCode = 1;
            return;
        }

        using var manager = new ContainerManager();

        // Check runtime availability
        if (!await manager.IsRuntimeAvailable())
        {
            SystemLogger.Error("Docker is not running or not accessible");
            SystemLogger.Error("Make sure Docker is installed and the Docker daemon is running");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            var state = await manager.Run(file.FullName);
            
            if (state.Status == ContainerStatus.Running)
            {
                Console.WriteLine();
                Console.WriteLine($"✓ Container '{state.Name}' is running");
                
                if (state.HostPort > 0)
                {
                    Console.WriteLine($"  → http://localhost:{state.HostPort}");
                }
            }
            else
            {
                SystemLogger.Warning($"Container state: {state.Status}");
            }
        }
        catch (SpecValidationException ex)
        {
            SystemLogger.Error("Invalid K42 script:");
            Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            SystemLogger.Error($"Failed to run container: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
