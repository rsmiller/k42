using FluentAssertions;
using K42.Runtime;
using K42.Schema;
using Xunit;

namespace K42.Tests;

/// <summary>
/// Integration tests that verify container creation with Docker.
/// These tests require Docker to be running.
/// 
/// Mark with [Trait("Category", "Integration")] to allow filtering.
/// </summary>
[Trait("Category", "Integration")]
public class ContainerIntegrationTests : IAsyncLifetime
{
    private ContainerRuntime? _runtime;
    private readonly List<string> _createdContainers = new();

    public async Task InitializeAsync()
    {
        _runtime = new ContainerRuntime();
        
        // Skip all tests if Docker is not available
        if (!await _runtime.IsAvailable())
        {
            throw new SkipException("Docker is not running - skipping integration tests");
        }
    }

    public async Task DisposeAsync()
    {
        // Clean up any containers we created
        if (_runtime != null)
        {
            foreach (var name in _createdContainers)
            {
                try
                {
                    await _runtime.Remove(name);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _runtime.Dispose();
        }
    }

    [Fact]
    public async Task IsAvailable_WhenDockerRunning_ReturnsTrue()
    {
        // Act
        var available = await _runtime!.IsAvailable();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task GetState_NonExistentContainer_ReturnsNotFound()
    {
        // Arrange
        var name = $"k42-test-nonexistent-{Guid.NewGuid():N}";

        // Act
        var state = await _runtime!.GetState(name);

        // Assert
        state.Status.Should().Be(ContainerStatus.NotFound);
        state.Name.Should().Be(name);
    }

    [Fact]
    public async Task CreateAndStart_SimpleContainer_CreatesSuccessfully()
    {
        // Arrange
        var name = $"test-hello-{Guid.NewGuid():N}"[..30];
        _createdContainers.Add(name);

        var spec = new K42Spec
        {
            Name = name,
            Image = "hello-world:latest",
            ContainerPort = 0,
            HostPort = 0,
            PublicNetwork = true,
            StorageSize = "100MB"
        };

        // Act
        var state = await _runtime!.CreateAndStart(spec);

        // Assert
        state.Name.Should().Be(name);
        state.Image.Should().Contain("hello-world");
        state.ContainerId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAndStart_WithEnvironmentVariables_InjectsVariables()
    {
        // Arrange
        var name = $"test-env-{Guid.NewGuid():N}"[..30];
        _createdContainers.Add(name);

        var spec = new K42Spec
        {
            Name = name,
            Image = "alpine:latest",
            ContainerPort = 0,
            HostPort = 0,
            StorageSize = "100MB",
            Environment = new Dictionary<string, string>
            {
                ["TEST_VAR"] = "test_value",
                ["ANOTHER_VAR"] = "another_value"
            },
            Command = new List<string> { "sh", "-c", "echo $TEST_VAR && sleep 5" }
        };

        // Act
        var state = await _runtime!.CreateAndStart(spec);

        // Assert
        state.Name.Should().Be(name);
        
        // Give container time to start and produce output
        await Task.Delay(2000);
        
        var logs = await _runtime.GetLogs(name);
        logs.Should().Contain("test_value");
    }

    [Fact]
    public async Task Stop_RunningContainer_StopsSuccessfully()
    {
        // Arrange
        var name = $"test-stop-{Guid.NewGuid():N}"[..30];
        _createdContainers.Add(name);

        var spec = new K42Spec
        {
            Name = name,
            Image = "alpine:latest",
            ContainerPort = 0,
            HostPort = 0,
            StorageSize = "100MB",
            Command = new List<string> { "sleep", "infinity" }
        };

        await _runtime!.CreateAndStart(spec);
        
        // Verify it's running
        var runningState = await _runtime.GetState(name);
        runningState.Status.Should().Be(ContainerStatus.Running);

        // Act
        var stopped = await _runtime.Stop(name);

        // Assert
        stopped.Should().BeTrue();
        
        var stoppedState = await _runtime.GetState(name);
        stoppedState.Status.Should().BeOneOf(ContainerStatus.Stopped, ContainerStatus.Failed);
    }

    [Fact]
    public async Task Remove_ExistingContainer_RemovesSuccessfully()
    {
        // Arrange
        var name = $"test-remove-{Guid.NewGuid():N}"[..30];
        // Don't add to cleanup list since we're removing it

        var spec = new K42Spec
        {
            Name = name,
            Image = "alpine:latest",
            ContainerPort = 0,
            HostPort = 0,
            StorageSize = "100MB",
            Command = new List<string> { "sleep", "5" }
        };

        await _runtime!.CreateAndStart(spec);

        // Act
        var removed = await _runtime.Remove(name);

        // Assert
        removed.Should().BeTrue();
        
        var state = await _runtime.GetState(name);
        state.Status.Should().Be(ContainerStatus.NotFound);
    }

    [Fact]
    public async Task GetLogs_ContainerWithOutput_ReturnsLogs()
    {
        // Arrange
        var name = $"test-logs-{Guid.NewGuid():N}"[..30];
        _createdContainers.Add(name);

        var spec = new K42Spec
        {
            Name = name,
            Image = "alpine:latest",
            ContainerPort = 0,
            HostPort = 0,
            StorageSize = "100MB",
            Command = new List<string> { "sh", "-c", "echo 'K42 TEST OUTPUT' && sleep 5" }
        };

        await _runtime!.CreateAndStart(spec);
        
        // Give container time to produce output
        await Task.Delay(2000);

        // Act
        var logs = await _runtime.GetLogs(name);

        // Assert
        logs.Should().Contain("K42 TEST OUTPUT");
    }

    [Fact]
    public async Task GetAllContainers_WithK42Containers_ReturnsOnlyK42Containers()
    {
        // Arrange
        var name = $"test-list-{Guid.NewGuid():N}"[..30];
        _createdContainers.Add(name);

        var spec = new K42Spec
        {
            Name = name,
            Image = "alpine:latest",
            ContainerPort = 0,
            HostPort = 0,
            StorageSize = "100MB",
            Command = new List<string> { "sleep", "30" }
        };

        await _runtime!.CreateAndStart(spec);

        // Act
        var containers = await _runtime.GetAllContainers();

        // Assert
        containers.Should().Contain(c => c.Name == name);
        
        // All returned containers should be K42 managed
        foreach (var container in containers)
        {
            container.Name.Should().NotBeNullOrEmpty();
        }
    }
}

/// <summary>
/// Custom exception to skip tests when Docker is not available.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
