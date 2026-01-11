using FluentAssertions;
using K42.Runtime;
using Xunit;

namespace K42.Tests;

/// <summary>
/// Tests for container state representation.
/// </summary>
public class ContainerStateTests
{
    [Fact]
    public void ContainerState_NewInstance_HasDefaults()
    {
        // Act
        var state = new ContainerState();

        // Assert
        state.Name.Should().BeEmpty();
        state.ContainerId.Should().BeEmpty();
        state.Image.Should().BeEmpty();
        state.Status.Should().Be(ContainerStatus.NotFound);
        state.HostPort.Should().Be(0);
        state.ContainerPort.Should().Be(0);
        state.PublicNetwork.Should().BeFalse();
        state.StartedAt.Should().BeNull();
        state.LastRestartAt.Should().BeNull();
        state.RestartCount.Should().Be(0);
        state.ExitReason.Should().BeNull();
    }

    [Fact]
    public void ContainerState_WithValues_PreservesValues()
    {
        // Arrange
        var startTime = DateTime.UtcNow;

        // Act
        var state = new ContainerState
        {
            Name = "test-container",
            ContainerId = "abc123def456",
            Image = "nginx:alpine",
            Status = ContainerStatus.Running,
            HostPort = 8080,
            ContainerPort = 80,
            PublicNetwork = true,
            StartedAt = startTime,
            RestartCount = 2
        };

        // Assert
        state.Name.Should().Be("test-container");
        state.ContainerId.Should().Be("abc123def456");
        state.Image.Should().Be("nginx:alpine");
        state.Status.Should().Be(ContainerStatus.Running);
        state.HostPort.Should().Be(8080);
        state.ContainerPort.Should().Be(80);
        state.PublicNetwork.Should().BeTrue();
        state.StartedAt.Should().Be(startTime);
        state.RestartCount.Should().Be(2);
    }

    [Theory]
    [InlineData(ContainerStatus.NotFound)]
    [InlineData(ContainerStatus.Stopped)]
    [InlineData(ContainerStatus.Running)]
    [InlineData(ContainerStatus.Creating)]
    [InlineData(ContainerStatus.Failed)]
    public void ContainerStatus_AllValuesExist(ContainerStatus status)
    {
        // Assert
        Enum.IsDefined(typeof(ContainerStatus), status).Should().BeTrue();
    }

    [Fact]
    public void ContainerState_FailedWithReason_HasExitReason()
    {
        // Act
        var state = new ContainerState
        {
            Name = "failed-container",
            Status = ContainerStatus.Failed,
            ExitReason = "Exit code: 137"
        };

        // Assert
        state.Status.Should().Be(ContainerStatus.Failed);
        state.ExitReason.Should().Be("Exit code: 137");
    }
}
