namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Data transfer object for media retention policy information.
    /// </summary>
    public class MediaRetentionPolicyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PositiveBalanceRetentionDays { get; set; }
        public int ZeroBalanceRetentionDays { get; set; }
        public int NegativeBalanceRetentionDays { get; set; }
        public int SoftDeleteGracePeriodDays { get; set; }
        public bool RespectRecentAccess { get; set; }
        public int RecentAccessWindowDays { get; set; }
        public bool IsDefault { get; set; }
        public long? MaxStorageSizeBytes { get; set; }
        public int? MaxFileCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int VirtualKeyGroupCount { get; set; }
    }

    /// <summary>
    /// Extended DTO for media retention policy with virtual key group details.
    /// </summary>
    public class MediaRetentionPolicyDetailDto : MediaRetentionPolicyDto
    {
        public List<VirtualKeyGroupSummaryDto> VirtualKeyGroups { get; set; } = new();
    }

    /// <summary>
    /// Summary information for a virtual key group.
    /// </summary>
    public class VirtualKeyGroupSummaryDto
    {
        public int Id { get; set; }
        public decimal Balance { get; set; }
        public int VirtualKeyCount { get; set; }
    }

    /// <summary>
    /// Request model for creating a new media retention policy.
    /// </summary>
    public class CreateMediaRetentionPolicyRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PositiveBalanceRetentionDays { get; set; }
        public int ZeroBalanceRetentionDays { get; set; }
        public int NegativeBalanceRetentionDays { get; set; }
        public int SoftDeleteGracePeriodDays { get; set; } = 7;
        public bool RespectRecentAccess { get; set; } = true;
        public int RecentAccessWindowDays { get; set; } = 7;
        public bool IsDefault { get; set; }
        public long? MaxStorageSizeBytes { get; set; }
        public int? MaxFileCount { get; set; }
    }

    /// <summary>
    /// Request model for updating an existing media retention policy.
    /// </summary>
    public class UpdateMediaRetentionPolicyRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? PositiveBalanceRetentionDays { get; set; }
        public int? ZeroBalanceRetentionDays { get; set; }
        public int? NegativeBalanceRetentionDays { get; set; }
        public int? SoftDeleteGracePeriodDays { get; set; }
        public bool? RespectRecentAccess { get; set; }
        public int? RecentAccessWindowDays { get; set; }
        public bool? IsDefault { get; set; }
        public long? MaxStorageSizeBytes { get; set; }
        public int? MaxFileCount { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Represents the result of a media cleanup operation.
    /// </summary>
    public class CleanupResultDto
    {
        public int VirtualKeyGroupId { get; set; }
        public bool DryRun { get; set; }
        public int MediaRecordsEvaluated { get; set; }
        public int MediaRecordsMarkedForDeletion { get; set; }
        public int MediaRecordsDeleted { get; set; }
        public long StorageBytesFreed { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
