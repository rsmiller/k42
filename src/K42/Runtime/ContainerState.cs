namespace K42.Runtime;

/// <summary>
/// Represents the state of a K42-managed container.
/// </summary>
public sealed class ContainerState
{
    public string Name { get; init; } = string.Empty;
    public string ContainerId { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public ContainerStatus Status { get; init; }
    public int HostPort { get; init; }
    public int ContainerPort { get; init; }
    public bool PublicNetwork { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? LastRestartAt { get; init; }
    public int RestartCount { get; init; }
    public string? ExitReason { get; init; }
}

/// <summary>
/// Simple container status. No reconciliation. No desired vs actual.
/// Just what it is.
/// </summary>
public enum ContainerStatus
{
    /// <summary>Container does not exist</summary>
    NotFound,
    
    /// <summary>Container exists but is not running</summary>
    Stopped,
    
    /// <summary>Container is running</summary>
    Running,
    
    /// <summary>Container is being created</summary>
    Creating,
    
    /// <summary>Container exited with error</summary>
    Failed
}
