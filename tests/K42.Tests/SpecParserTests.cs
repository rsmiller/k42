using FluentAssertions;
using K42.Schema;
using Xunit;

namespace K42.Tests;

/// <summary>
/// Tests for YAML parsing and validation.
/// These tests verify that K42 correctly reads and validates YAML specifications.
/// </summary>
public class SpecParserTests
{
    private readonly SpecParser _parser = new();

    #region Basic Parsing

    [Fact]
    public void Parse_MinimalValidYaml_ReturnsSpec()
    {
        // Arrange
        var yaml = @"
name: my-container
image: nginx:alpine
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.Name.Should().Be("my-container");
        spec.Image.Should().Be("nginx:alpine");
    }

    [Fact]
    public void Parse_FullYaml_ReturnsAllFields()
    {
        // Arrange
        var yaml = @"
name: production-api
image: myregistry.com/api:v2.1.0
container-port: 3000
host-port: 8080
public-network: false
storage-size: 5GB
workdir: /app
environment:
  NODE_ENV: production
  DATABASE_URL: postgresql://localhost/db
command:
  - node
  - server.js
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.Name.Should().Be("production-api");
        spec.Image.Should().Be("myregistry.com/api:v2.1.0");
        spec.ContainerPort.Should().Be(3000);
        spec.HostPort.Should().Be(8080);
        spec.PublicNetwork.Should().BeFalse();
        spec.StorageSize.Should().Be("5GB");
        spec.WorkDir.Should().Be("/app");
        spec.Environment.Should().ContainKey("NODE_ENV").WhoseValue.Should().Be("production");
        spec.Environment.Should().ContainKey("DATABASE_URL").WhoseValue.Should().Be("postgresql://localhost/db");
        spec.Command.Should().BeEquivalentTo(new[] { "node", "server.js" });
    }

    [Fact]
    public void Parse_WithDefaults_AppliesDefaultValues()
    {
        // Arrange
        var yaml = @"
name: test
image: hello-world
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.ContainerPort.Should().Be(80, "default container port is 80");
        spec.HostPort.Should().Be(80, "default host port is 80");
        spec.PublicNetwork.Should().BeTrue("default is public");
        spec.StorageSize.Should().Be("1GB", "default storage is 1GB");
        spec.Environment.Should().BeNull("no environment by default");
        spec.Command.Should().BeNull("no command override by default");
        spec.WorkDir.Should().BeNull("no workdir by default");
    }

    #endregion

    #region Script File Extraction

    [Fact]
    public void ExtractYaml_WithMarkers_ExtractsCorrectly()
    {
        // Arrange
        var script = @"#!/usr/bin/env k42
# ---K42-START---
# name: my-service
# image: nginx:alpine
# host-port: 8080
# ---K42-END---
# This is a comment after
";

        // Act
        var yaml = _parser.ExtractYaml(script);

        // Assert
        yaml.Should().Contain("name: my-service");
        yaml.Should().Contain("image: nginx:alpine");
        yaml.Should().Contain("host-port: 8080");
        yaml.Should().NotContain("#!/usr/bin/env");
        yaml.Should().NotContain("This is a comment after");
    }

    [Fact]
    public void ExtractYaml_WithoutMarkers_TreatsEntireFileAsYaml()
    {
        // Arrange
        var script = @"#!/usr/bin/env k42
name: my-service
image: nginx:alpine
";

        // Act
        var yaml = _parser.ExtractYaml(script);

        // Assert
        yaml.Should().Contain("name: my-service");
        yaml.Should().Contain("image: nginx:alpine");
    }

    [Fact]
    public void ExtractYaml_PlainYamlFile_WorksCorrectly()
    {
        // Arrange
        var yaml = @"name: plain-service
image: redis:7
host-port: 6379
";

        // Act
        var extracted = _parser.ExtractYaml(yaml);

        // Assert
        extracted.Should().Contain("name: plain-service");
        extracted.Should().Contain("image: redis:7");
    }

    #endregion

    #region Validation - Required Fields

    [Fact]
    public void Parse_MissingName_ThrowsValidationException()
    {
        // Arrange
        var yaml = @"
image: nginx:alpine
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*name*required*");
    }

    [Fact]
    public void Parse_EmptyName_ThrowsValidationException()
    {
        // Arrange
        var yaml = @"
name: """"
image: nginx:alpine
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Parse_MissingImage_ThrowsValidationException()
    {
        // Arrange
        var yaml = @"
name: my-container
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*image*required*");
    }

    #endregion

    #region Validation - Name Format

    [Theory]
    [InlineData("valid-name")]
    [InlineData("valid_name")]
    [InlineData("valid123")]
    [InlineData("a")]
    [InlineData("a-b-c")]
    [InlineData("123abc")]
    public void Parse_ValidName_Succeeds(string name)
    {
        // Arrange
        var yaml = $@"
name: {name}
image: nginx:alpine
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.Name.Should().Be(name);
    }

    [Theory]
    [InlineData("-invalid", "cannot start with hyphen")]
    [InlineData("_invalid", "cannot start with underscore")]
    [InlineData("Invalid", "uppercase not allowed")]
    [InlineData("in valid", "spaces not allowed")]
    [InlineData("in.valid", "dots not allowed")]
    public void Parse_InvalidName_ThrowsValidationException(string name, string reason)
    {
        // Arrange
        var yaml = $@"
name: {name}
image: nginx:alpine
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>(reason)
            .WithMessage("*name*");
    }

    #endregion

    #region Validation - Ports

    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    [InlineData(443)]
    [InlineData(8080)]
    [InlineData(65535)]
    public void Parse_ValidPort_Succeeds(int port)
    {
        // Arrange
        var yaml = $@"
name: test
image: nginx
host-port: {port}
container-port: {port}
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.HostPort.Should().Be(port);
        spec.ContainerPort.Should().Be(port);
    }

    [Fact]
    public void Parse_NegativePort_ThrowsValidationException()
    {
        // Arrange
        var yaml = @"
name: test
image: nginx
host-port: -1
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*port*");
    }

    [Fact]
    public void Parse_PortTooHigh_ThrowsValidationException()
    {
        // Arrange
        var yaml = @"
name: test
image: nginx
host-port: 65536
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*port*");
    }

    #endregion

    #region Validation - Storage Size

    [Theory]
    [InlineData("100MB")]
    [InlineData("500MB")]
    [InlineData("1GB")]
    [InlineData("10GB")]
    [InlineData("100GB")]
    public void Parse_ValidStorageSize_Succeeds(string size)
    {
        // Arrange
        var yaml = $@"
name: test
image: nginx
storage-size: {size}
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.StorageSize.Should().Be(size);
    }

    [Theory]
    [InlineData("100KB", "KB not supported")]
    [InlineData("1TB", "TB not supported")]
    [InlineData("invalid", "not a valid size")]
    [InlineData("100", "missing unit")]
    [InlineData("-1GB", "negative not allowed")]
    [InlineData("0GB", "zero not allowed")]
    public void Parse_InvalidStorageSize_ThrowsValidationException(string size, string reason)
    {
        // Arrange
        var yaml = $@"
name: test
image: nginx
storage-size: {size}
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>(reason)
            .WithMessage("*storage-size*");
    }

    #endregion

    #region Validation - YAML Syntax

    [Fact]
    public void Parse_InvalidYamlSyntax_ThrowsValidationException()
    {
        // Arrange
        var yaml = @"
name: test
  image: nginx  # bad indentation
";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*Invalid YAML syntax*");
    }

    [Fact]
    public void Parse_EmptyYaml_ThrowsValidationException()
    {
        // Arrange
        var yaml = "";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void Parse_WhitespaceOnlyYaml_ThrowsValidationException()
    {
        // Arrange
        var yaml = "   \n\n   \t   ";

        // Act
        var act = () => _parser.Parse(yaml);

        // Assert
        act.Should().Throw<SpecValidationException>()
            .WithMessage("*empty*");
    }

    #endregion

    #region Storage Size Parsing

    [Theory]
    [InlineData("1GB", 1024L * 1024 * 1024)]
    [InlineData("500MB", 500L * 1024 * 1024)]
    [InlineData("10GB", 10L * 1024 * 1024 * 1024)]
    public void ParseStorageSize_ReturnsCorrectBytes(string size, long expectedBytes)
    {
        // Act
        var bytes = SpecParser.ParseStorageSize(size);

        // Assert
        bytes.Should().Be(expectedBytes);
    }

    #endregion

    #region Environment Variables

    [Fact]
    public void Parse_EnvironmentVariables_PreservesValues()
    {
        // Arrange
        var yaml = @"
name: test
image: nginx
environment:
  SIMPLE: value
  WITH_EQUALS: key=value
  WITH_COLON: 'http://localhost:8080'
  EMPTY_VALUE: ''
  NUMERIC: '123'
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.Environment.Should().HaveCount(5);
        spec.Environment!["SIMPLE"].Should().Be("value");
        spec.Environment["WITH_EQUALS"].Should().Be("key=value");
        spec.Environment["WITH_COLON"].Should().Be("http://localhost:8080");
        spec.Environment["EMPTY_VALUE"].Should().BeEmpty();
        spec.Environment["NUMERIC"].Should().Be("123");
    }

    [Fact]
    public void Parse_EnvironmentWithSecrets_PreservesPlainText()
    {
        // This test verifies that K42 does NOT do any secret interpolation
        // Secrets are plain text by design
        
        // Arrange
        var yaml = @"
name: test
image: nginx
environment:
  DATABASE_PASSWORD: supersecret123
  API_KEY: sk-1234567890abcdef
  CONNECTION_STRING: 'Server=localhost;Password=mypass'
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.Environment!["DATABASE_PASSWORD"].Should().Be("supersecret123");
        spec.Environment["API_KEY"].Should().Be("sk-1234567890abcdef");
        spec.Environment["CONNECTION_STRING"].Should().Be("Server=localhost;Password=mypass");
    }

    #endregion

    #region Command Parsing

    [Fact]
    public void Parse_CommandAsList_PreservesOrder()
    {
        // Arrange
        var yaml = @"
name: test
image: nginx
command:
  - /bin/sh
  - -c
  - 'echo hello && sleep infinity'
";

        // Act
        var spec = _parser.Parse(yaml);

        // Assert
        spec.Command.Should().BeEquivalentTo(
            new[] { "/bin/sh", "-c", "echo hello && sleep infinity" },
            options => options.WithStrictOrdering());
    }

    #endregion
}
