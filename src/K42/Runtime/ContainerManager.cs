using K42.Logging;
using K42.Schema;
using System.Text.Json;

namespace K42.Runtime;

/// <summary>
/// Manages K42 container registrations.
/// 
/// This is NOT a controller. This is NOT a reconciliation loop.
/// This is a registry that tracks what K42 scripts have been executed.
/// 
/// The file system is the source of truth for registrations.
/// The container runtime is the source of truth for container state.
/// </summary>
public sealed class ContainerManager : IDisposable
{
    private readonly ContainerRuntime _runtime;
    private readonly string _registrationDir;

    public ContainerManager()
    {
        _runtime = new ContainerRuntime();
        _registrationDir = GetRegistrationDirectory();
        
        if (!Directory.Exists(_registrationDir))
        {
            Directory.CreateDirectory(_registrationDir);
        }
    }

    private static string GetRegistrationDirectory()
    {
        // Linux: /var/lib/k42
        // Windows: C:\ProgramData\K42
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "K42",
                "registrations");
        }
        else
        {
            return "/var/lib/k42/registrations";
        }
    }

    /// <summary>
    /// Check if Docker/containerd is available.
    /// </summary>
    public Task<bool> IsRuntimeAvailable() => _runtime.IsAvailable();

    /// <summary>
    /// Run a K42 script file.
    /// 
    /// Behavior:
    /// - If container already exists and running → do nothing
    /// - If container exists but stopped → start it
    /// - If container doesn't exist → create from YAML
    /// </summary>
    public async Task<ContainerState> Run(string scriptPath)
    {
        // Parse the script
        var parser = new SpecParser();
        var spec = parser.ParseFile(scriptPath);

        SystemLogger.Info($"Loaded spec: {spec.Name}");
        SystemLogger.Info($"  Image: {spec.Image}");
        SystemLogger.Info($"  Port: {spec.ContainerPort} → {spec.HostPort}");

        // Check current state
        var currentState = await _runtime.GetState(spec.Name);

        switch (currentState.Status)
        {
            case ContainerStatus.Running:
                SystemLogger.Info($"Container '{spec.Name}' is already running");
                return currentState;

            case ContainerStatus.Stopped:
            case ContainerStatus.Failed:
                SystemLogger.Info($"Container '{spec.Name}' exists but is stopped, removing and recreating");
                await _runtime.Remove(spec.Name);
                break;

            case ContainerStatus.NotFound:
                SystemLogger.Info($"Container '{spec.Name}' does not exist, creating");
                break;
        }

        // Create and start
        var state = await _runtime.CreateAndStart(spec);

        // Register the script
        await Register(spec.Name, scriptPath, spec);

        SystemLogger.Info($"Container '{spec.Name}' started successfully");
        if (state.HostPort > 0)
        {
            var bindAddr = spec.PublicNetwork ? "0.0.0.0" : "127.0.0.1";
            SystemLogger.Info($"  Listening on {bindAddr}:{state.HostPort}");
        }

        return state;
    }

    /// <summary>
    /// Get status of a container by name.
    /// </summary>
    public async Task<ContainerState> GetStatus(string name)
    {
        return await _runtime.GetState(name);
    }

    /// <summary>
    /// Get all K42 containers.
    /// </summary>
    public async Task<List<ContainerState>> GetAllContainers()
    {
        return await _runtime.GetAllContainers();
    }

    /// <summary>
    /// Stop a container.
    /// </summary>
    public async Task<bool> Stop(string name)
    {
        return await _runtime.Stop(name);
    }

    /// <summary>
    /// Stop and remove a container, including its registration.
    /// </summary>
    public async Task<bool> Unregister(string name)
    {
        var removed = await _runtime.Remove(name);
        
        // Remove registration file
        var regFile = Path.Combine(_registrationDir, $"{name}.json");
        if (File.Exists(regFile))
        {
            File.Delete(regFile);
        }

        return removed;
    }

    /// <summary>
    /// Get container logs.
    /// </summary>
    public async Task<string> GetLogs(string name, int tailLines = 100)
    {
        return await _runtime.GetLogs(name, tailLines);
    }

    /// <summary>
    /// Get registration info for a container.
    /// </summary>
    public Registration? GetRegistration(string name)
    {
        var regFile = Path.Combine(_registrationDir, $"{name}.json");
        if (!File.Exists(regFile))
            return null;

        var json = File.ReadAllText(regFile);
        return JsonSerializer.Deserialize<Registration>(json);
    }

    /// <summary>
    /// Get all registrations.
    /// </summary>
    public List<Registration> GetAllRegistrations()
    {
        var registrations = new List<Registration>();

        if (!Directory.Exists(_registrationDir))
            return registrations;

        foreach (var file in Directory.GetFiles(_registrationDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var reg = JsonSerializer.Deserialize<Registration>(json);
                if (reg != null)
                    registrations.Add(reg);
            }
            catch
            {
                // Skip corrupted registration files
            }
        }

        return registrations;
    }

    private async Task Register(string name, string scriptPath, K42Spec spec)
    {
        var registration = new Registration
        {
            Name = name,
            ScriptPath = Path.GetFullPath(scriptPath),
            Image = spec.Image,
            RegisteredAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(registration, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var regFile = Path.Combine(_registrationDir, $"{name}.json");
        await File.WriteAllTextAsync(regFile, json);
    }

    public void Dispose()
    {
        _runtime.Dispose();
    }
}

/// <summary>
/// Registration information stored on disk.
/// </summary>
public sealed class Registration
{
    public string Name { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
