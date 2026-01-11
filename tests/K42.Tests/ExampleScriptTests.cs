using FluentAssertions;
using K42.Schema;
using Xunit;

namespace K42.Tests;

/// <summary>
/// Tests that verify the example K42 scripts in the examples directory are valid.
/// </summary>
public class ExampleScriptTests
{
    private readonly SpecParser _parser = new();
    private readonly string _examplesDir;

    public ExampleScriptTests()
    {
        // Find the examples directory relative to the test assembly
        var currentDir = Directory.GetCurrentDirectory();
        
        // Walk up to find the project root
        var dir = new DirectoryInfo(currentDir);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "examples")))
        {
            dir = dir.Parent;
        }

        _examplesDir = dir != null 
            ? Path.Combine(dir.FullName, "examples") 
            : Path.Combine(currentDir, "..", "..", "..", "..", "..", "examples");
    }

    [SkippableFact]
    public void NginxExample_IsValidK42Spec()
    {
        // Arrange
        var filePath = Path.Combine(_examplesDir, "nginx.k42");
        Skip.If(!File.Exists(filePath), "Example file not found");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("nginx-hello");
        spec.Image.Should().Be("nginx:alpine");
        spec.ContainerPort.Should().Be(80);
        spec.HostPort.Should().Be(8080);
        spec.PublicNetwork.Should().BeTrue();
        spec.StorageSize.Should().Be("500MB");
    }

    [SkippableFact]
    public void RedisExample_IsValidK42Spec()
    {
        // Arrange
        var filePath = Path.Combine(_examplesDir, "redis.k42");
        Skip.If(!File.Exists(filePath), "Example file not found");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("redis-cache");
        spec.Image.Should().Be("redis:7-alpine");
        spec.ContainerPort.Should().Be(6379);
        spec.HostPort.Should().Be(6379);
        spec.PublicNetwork.Should().BeFalse();
        spec.Command.Should().NotBeNull();
        spec.Command.Should().Contain("redis-server");
    }

    [SkippableFact]
    public void SimpleYamlExample_IsValidK42Spec()
    {
        // Arrange
        var filePath = Path.Combine(_examplesDir, "simple.yaml");
        Skip.If(!File.Exists(filePath), "Example file not found");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("hello-world");
        spec.Image.Should().Be("hello-world:latest");
        spec.ContainerPort.Should().Be(0);
        spec.HostPort.Should().Be(0);
    }

    [SkippableFact]
    public void FullAppExample_IsValidK42Spec()
    {
        // Arrange
        var filePath = Path.Combine(_examplesDir, "full-app.k42");
        Skip.If(!File.Exists(filePath), "Example file not found");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("my-app");
        spec.Image.Should().Contain("myregistry.com");
        spec.ContainerPort.Should().Be(3000);
        spec.HostPort.Should().Be(80);
        spec.StorageSize.Should().Be("2GB");
        spec.Environment.Should().NotBeNull();
        spec.Environment.Should().ContainKey("NODE_ENV");
        spec.Environment.Should().ContainKey("DATABASE_URL");
    }

    [SkippableFact]
    public void AllExampleFiles_AreValidK42Specs()
    {
        // Skip if examples directory doesn't exist
        Skip.If(!Directory.Exists(_examplesDir), "Examples directory not found");

        // Arrange
        var files = Directory.GetFiles(_examplesDir, "*.k42")
            .Concat(Directory.GetFiles(_examplesDir, "*.yaml"))
            .ToList();

        Skip.If(files.Count == 0, "No example files found");

        // Act & Assert
        foreach (var file in files)
        {
            var act = () => _parser.ParseFile(file);
            act.Should().NotThrow($"File {Path.GetFileName(file)} should be valid");
        }
    }
}

/// <summary>
/// Attribute for skippable facts (tests that can be skipped based on conditions).
/// </summary>
public class SkippableFactAttribute : FactAttribute { }

/// <summary>
/// Helper class for skipping tests.
/// </summary>
public static class Skip
{
    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new SkipException(reason);
        }
    }
}
