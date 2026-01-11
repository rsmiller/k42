using FluentAssertions;
using K42.Schema;
using Xunit;

namespace K42.Tests;

/// <summary>
/// Tests for reading actual K42 script files from disk.
/// </summary>
public class FileParsingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SpecParser _parser = new();

    public FileParsingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"k42-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ParseFile_ValidYamlFile_ReturnsSpec()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.yaml");
        File.WriteAllText(filePath, @"
name: file-test
image: nginx:alpine
host-port: 9090
");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("file-test");
        spec.Image.Should().Be("nginx:alpine");
        spec.HostPort.Should().Be(9090);
    }

    [Fact]
    public void ParseFile_K42ScriptWithShebang_ReturnsSpec()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test.k42");
        File.WriteAllText(filePath, @"#!/usr/bin/env k42
# ---K42-START---
# name: script-test
# image: redis:7
# container-port: 6379
# host-port: 6379
# public-network: false
# ---K42-END---
# Additional comments here
");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("script-test");
        spec.Image.Should().Be("redis:7");
        spec.ContainerPort.Should().Be(6379);
        spec.PublicNetwork.Should().BeFalse();
    }

    [Fact]
    public void ParseFile_NonExistentFile_ThrowsValidationException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "does-not-exist.yaml");

        // Act
        var act = () => _parser.ParseFile(filePath);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void ParseFile_ComplexK42Script_ParsesAllFields()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "complex.k42");
        File.WriteAllText(filePath, @"#!/usr/bin/env k42
# ---K42-START---
# name: complex-app
# image: myregistry.com/myapp:v2.0.0
# container-port: 3000
# host-port: 80
# public-network: true
# storage-size: 10GB
# workdir: /app
# environment:
#   NODE_ENV: production
#   DATABASE_URL: postgresql://user:pass@localhost:5432/db
#   REDIS_URL: redis://localhost:6379
# command:
#   - npm
#   - start
# ---K42-END---
#
# This is a production application.
# Deploy with: k42 run ./complex.k42
#
");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("complex-app");
        spec.Image.Should().Be("myregistry.com/myapp:v2.0.0");
        spec.ContainerPort.Should().Be(3000);
        spec.HostPort.Should().Be(80);
        spec.PublicNetwork.Should().BeTrue();
        spec.StorageSize.Should().Be("10GB");
        spec.WorkDir.Should().Be("/app");
        spec.Environment.Should().HaveCount(3);
        spec.Environment!["NODE_ENV"].Should().Be("production");
        spec.Command.Should().BeEquivalentTo(new[] { "npm", "start" });
    }

    [Fact]
    public void ParseFile_YamlWithComments_IgnoresComments()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "commented.yaml");
        File.WriteAllText(filePath, @"
# This is the container name
name: commented-test

# Docker image to use
image: alpine:latest

# Port configuration
host-port: 8080  # Web port
container-port: 80  # Internal port
");

        // Act
        var spec = _parser.ParseFile(filePath);

        // Assert
        spec.Name.Should().Be("commented-test");
        spec.Image.Should().Be("alpine:latest");
        spec.HostPort.Should().Be(8080);
        spec.ContainerPort.Should().Be(80);
    }

    [Fact]
    public void ParseFile_EmptyFile_ThrowsValidationException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "empty.yaml");
        File.WriteAllText(filePath, "");

        // Act
        var act = () => _parser.ParseFile(filePath);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void ParseFile_OnlyShebang_ThrowsValidationException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "shebang-only.k42");
        File.WriteAllText(filePath, "#!/usr/bin/env k42\n");

        // Act
        var act = () => _parser.ParseFile(filePath);

        // Assert
        act.Should().Throw<SpecValidationException>();
    }
}
