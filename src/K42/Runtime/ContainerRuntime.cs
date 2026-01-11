using Docker.DotNet;
using Docker.DotNet.Models;
using K42.Logging;
using K42.Schema;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace K42.Runtime;

/// <summary>
/// Docker/containerd interface.
/// 
/// K42 uses Docker explicitly. Not abstracted. Not hidden.
/// This is the only place that talks to the container runtime.
/// </summary>
public sealed class ContainerRuntime : IDisposable
{
    private readonly DockerClient _client;
    private const string K42Label = "k42.managed";
    private const string K42NameLabel = "k42.name";
    private const string K42PortLabel = "k42.host-port";

    public ContainerRuntime()
    {
        // Connect to Docker daemon
        var dockerUri = GetDockerUri();
        _client = new DockerClientConfiguration(dockerUri).CreateClient();
    }

    private static Uri GetDockerUri()
    {
        // Linux: unix socket
        // Windows: named pipe
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new Uri("npipe://./pipe/docker_engine");
        }
        else
        {
            return new Uri("unix:///var/run/docker.sock");
        }
    }

    /// <summary>
    /// Check if Docker is available and responding.
    /// </summary>
    public async Task<bool> IsAvailable()
    {
        try
        {
            await _client.System.PingAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get state of a container by K42 name.
    /// </summary>
    public async Task<ContainerState> GetState(string name)
    {
        var container = await FindContainerByName(name);
        
        if (container == null)
        {
            return new ContainerState
            {
                Name = name,
                Status = ContainerStatus.NotFound
            };
        }

        var status = container.State switch
        {
            "running" => ContainerStatus.Running,
            "created" => ContainerStatus.Creating,
            "exited" => container.Status.Contains("Exited (0)") 
                ? ContainerStatus.Stopped 
                : ContainerStatus.Failed,
            "dead" => ContainerStatus.Failed,
            _ => ContainerStatus.Stopped
        };

        // Extract port from labels
        var hostPort = 0;
        if (container.Labels.TryGetValue(K42PortLabel, out var portStr))
            int.TryParse(portStr, out hostPort);

        // Get detailed inspection for restart count
        var inspection = await _client.Containers.InspectContainerAsync(container.ID);

        // Parse StartedAt from string
        DateTime? startedAt = null;
        if (!string.IsNullOrEmpty(inspection.State.StartedAt) && 
            DateTime.TryParse(inspection.State.StartedAt, out var parsedStart))
        {
            startedAt = parsedStart;
        }

        return new ContainerState
        {
            Name = name,
            ContainerId = container.ID[..12],
            Image = container.Image,
            Status = status,
            HostPort = hostPort,
            StartedAt = startedAt,
            RestartCount = (int)inspection.RestartCount,
            ExitReason = status == ContainerStatus.Failed 
                ? $"Exit code: {inspection.State.ExitCode}" 
                : null
        };
    }

    /// <summary>
    /// Get all K42-managed containers.
    /// </summary>
    public async Task<List<ContainerState>> GetAllContainers()
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool>
                    {
                        [K42Label] = true
                    }
                }
            });

        var states = new List<ContainerState>();

        foreach (var container in containers)
        {
            var name = container.Labels.TryGetValue(K42NameLabel, out var n) ? n : "unknown";
            var state = await GetState(name);
            states.Add(state);
        }

        return states;
    }

    /// <summary>
    /// Create and start a container from K42 spec.
    /// </summary>
    public async Task<ContainerState> CreateAndStart(K42Spec spec)
    {
        SystemLogger.Info($"Pulling image: {spec.Image}");
        
        // Always pull the image
        await PullImage(spec.Image);

        // Find available port
        var hostPort = spec.HostPort;
        if (hostPort > 0)
        {
            hostPort = await FindAvailablePort(hostPort);
            if (hostPort != spec.HostPort)
            {
                SystemLogger.Info($"Port {spec.HostPort} in use, using port {hostPort}");
            }
        }

        // Create volume for persistent storage
        var volumeName = $"k42-{spec.Name}-data";
        await EnsureVolume(volumeName, spec.StorageSize);

        // Build container configuration
        var hostConfig = new HostConfig
        {
            NetworkMode = "host",
            RestartPolicy = new RestartPolicy
            {
                Name = RestartPolicyKind.OnFailure,
                MaximumRetryCount = 5
            },
            Mounts = new List<Mount>
            {
                new Mount
                {
                    Type = "volume",
                    Source = volumeName,
                    Target = "/data",
                    ReadOnly = false
                }
            }
        };

        // Port bindings (even with host network, we track what port is intended)
        var portBindings = new Dictionary<string, IList<PortBinding>>();
        if (hostPort > 0)
        {
            var bindAddress = spec.PublicNetwork ? "0.0.0.0" : "127.0.0.1";
            portBindings[$"{spec.ContainerPort}/tcp"] = new List<PortBinding>
            {
                new PortBinding
                {
                    HostIP = bindAddress,
                    HostPort = hostPort.ToString()
                }
            };
            hostConfig.PortBindings = portBindings;
        }

        // Environment variables
        var env = spec.Environment?
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList() ?? new List<string>();

        // Add K42 metadata to environment
        env.Add($"K42_NAME={spec.Name}");
        env.Add($"K42_PORT={hostPort}");

        var createParams = new CreateContainerParameters
        {
            Name = $"k42-{spec.Name}",
            Image = spec.Image,
            Env = env,
            HostConfig = hostConfig,
            Labels = new Dictionary<string, string>
            {
                [K42Label] = "true",
                [K42NameLabel] = spec.Name,
                [K42PortLabel] = hostPort.ToString()
            },
            WorkingDir = spec.WorkDir
        };

        if (spec.Command != null && spec.Command.Count > 0)
        {
            createParams.Cmd = spec.Command;
        }

        SystemLogger.Info($"Creating container: k42-{spec.Name}");
        
        var response = await _client.Containers.CreateContainerAsync(createParams);
        
        SystemLogger.Info($"Starting container: {response.ID[..12]}");
        
        await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());

        return await GetState(spec.Name);
    }

    /// <summary>
    /// Stop a container gracefully.
    /// </summary>
    public async Task<bool> Stop(string name, int timeoutSeconds = 10)
    {
        var container = await FindContainerByName(name);
        if (container == null)
            return false;

        try
        {
            await _client.Containers.StopContainerAsync(
                container.ID,
                new ContainerStopParameters
                {
                    WaitBeforeKillSeconds = (uint)timeoutSeconds
                });
            return true;
        }
        catch (Exception ex)
        {
            SystemLogger.Error($"Failed to stop container: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Remove a container and its associated volume.
    /// </summary>
    public async Task<bool> Remove(string name)
    {
        var container = await FindContainerByName(name);
        if (container == null)
            return false;

        try
        {
            // Stop first if running
            if (container.State == "running")
            {
                await Stop(name);
            }

            // Remove container
            await _client.Containers.RemoveContainerAsync(
                container.ID,
                new ContainerRemoveParameters { Force = true });

            // Remove volume
            var volumeName = $"k42-{name}-data";
            try
            {
                await _client.Volumes.RemoveAsync(volumeName);
            }
            catch
            {
                // Volume might not exist, that's fine
            }

            return true;
        }
        catch (Exception ex)
        {
            SystemLogger.Error($"Failed to remove container: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get container logs.
    /// </summary>
    public async Task<string> GetLogs(string name, int tailLines = 100)
    {
        var container = await FindContainerByName(name);
        if (container == null)
            return $"Container not found: {name}";

        try
        {
            var stream = await _client.Containers.GetContainerLogsAsync(
                container.ID,
                false,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Tail = tailLines.ToString(),
                    Timestamps = true
                });

            // Read from multiplexed stream
            var buffer = new byte[81920];
            var result = new System.Text.StringBuilder();
            
            while (true)
            {
                var readResult = await stream.ReadOutputAsync(buffer, 0, buffer.Length, default);
                if (readResult.EOF)
                    break;
                
                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, readResult.Count);
                result.Append(text);
            }
            
            return CleanDockerLogs(result.ToString());
        }
        catch (Exception ex)
        {
            return $"Error getting logs: {ex.Message}";
        }
    }

    private async Task PullImage(string imageName)
    {
        try
        {
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = imageName
                },
                null,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                    {
                        // Only log meaningful progress
                        if (msg.Status.Contains("Pulling") || 
                            msg.Status.Contains("Downloaded") ||
                            msg.Status.Contains("Pull complete"))
                        {
                            SystemLogger.Info($"  {msg.Status}");
                        }
                    }
                }));
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to pull image '{imageName}': {ex.Message}");
        }
    }

    private async Task EnsureVolume(string volumeName, string size)
    {
        try
        {
            await _client.Volumes.InspectAsync(volumeName);
            // Volume exists
        }
        catch
        {
            // Create volume
            await _client.Volumes.CreateAsync(new VolumesCreateParameters
            {
                Name = volumeName,
                Labels = new Dictionary<string, string>
                {
                    [K42Label] = "true",
                    ["k42.size"] = size
                }
            });
        }
    }

    private async Task<ContainerListResponse?> FindContainerByName(string name)
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool>
                    {
                        [$"{K42NameLabel}={name}"] = true
                    }
                }
            });

        return containers.FirstOrDefault();
    }

    private async Task<int> FindAvailablePort(int startPort)
    {
        var port = startPort;
        
        while (port <= 65535)
        {
            if (await IsPortAvailable(port))
                return port;
            port++;
        }

        throw new Exception($"No available ports starting from {startPort}");
    }

    private Task<bool> IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string CleanDockerLogs(string logs)
    {
        // Docker multiplexed stream has 8-byte headers per frame
        // For simplicity, just remove non-printable characters at line starts
        var lines = logs.Split('\n');
        var cleaned = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
                continue;

            // Skip header bytes (first 8 bytes of each frame)
            var clean = line.Length > 8 
                ? line[8..].TrimStart('\0', '\x01', '\x02') 
                : line;

            if (!string.IsNullOrWhiteSpace(clean))
                cleaned.Add(clean);
        }

        return string.Join('\n', cleaned);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
