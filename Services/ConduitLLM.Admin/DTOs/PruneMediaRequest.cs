namespace ConduitLLM.Admin.DTOs;

/// <summary>Request model for pruning old media.</summary>
public sealed class PruneMediaRequest
{
    /// <summary>Gets or sets the number of days to keep media files.</summary>
    public int? DaysToKeep { get; set; }
}
