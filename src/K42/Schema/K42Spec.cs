using YamlDotNet.Serialization;

namespace K42.Schema;

/// <summary>
/// The complete K42 container specification.
/// This is the sole source of truth for a container.
/// 
/// All fields are explicit. No interpolation. No magic.
/// </summary>
public sealed class K42Spec
{
    /// <summary>
    /// Container name. Must be unique on this host.
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Container image to run. Always pulled on first run.
    /// </summary>
    [YamlMember(Alias = "image")]
    public string Image { get; set; } = string.Empty;

    /// <summary>
    /// Port inside the container. Default: 80
    /// </summary>
    [YamlMember(Alias = "container-port")]
    public int ContainerPort { get; set; } = 80;

    /// <summary>
    /// Port on the host. Default: 80.
    /// If taken, auto-increments to next available.
    /// Set to 0 to disable port binding.
    /// </summary>
    [YamlMember(Alias = "host-port")]
    public int HostPort { get; set; } = 80;

    /// <summary>
    /// Whether the container is publicly accessible.
    /// Default: true (public)
    /// Set to false to bind to 127.0.0.1 only.
    /// </summary>
    [YamlMember(Alias = "public-network")]
    public bool PublicNetwork { get; set; } = true;

    /// <summary>
    /// Persistent storage size. Default: 1GB
    /// Format: "500MB", "1GB", "10GB"
    /// </summary>
    [YamlMember(Alias = "storage-size")]
    public string StorageSize { get; set; } = "1GB";

    /// <summary>
    /// Environment variables. Plain text values only.
    /// No interpolation. No secrets abstraction.
    /// </summary>
    [YamlMember(Alias = "environment")]
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Command to run inside the container.
    /// If not specified, uses the image default.
    /// </summary>
    [YamlMember(Alias = "command")]
    public List<string>? Command { get; set; }

    /// <summary>
    /// Working directory inside the container.
    /// </summary>
    [YamlMember(Alias = "workdir")]
    public string? WorkDir { get; set; }
}
