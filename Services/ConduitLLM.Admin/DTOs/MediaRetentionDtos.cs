using ConduitLLM.Configuration.Entities;
using System.Text.Json.Serialization;

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
        public MediaQuotaExceededBehavior QuotaExceededBehavior { get; set; }
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
        public MediaQuotaExceededBehavior QuotaExceededBehavior { get; set; } =
            MediaQuotaExceededBehavior.Reject;
    }

    /// <summary>
    /// Request model for updating an existing media retention policy.
    /// </summary>
    public class UpdateMediaRetentionPolicyRequest
    {
        private long? _maxStorageSizeBytes;
        private int? _maxFileCount;

        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? PositiveBalanceRetentionDays { get; set; }
        public int? ZeroBalanceRetentionDays { get; set; }
        public int? NegativeBalanceRetentionDays { get; set; }
        public int? SoftDeleteGracePeriodDays { get; set; }
        public bool? RespectRecentAccess { get; set; }
        public int? RecentAccessWindowDays { get; set; }
        public bool? IsDefault { get; set; }
        public long? MaxStorageSizeBytes
        {
            get => _maxStorageSizeBytes;
            set
            {
                _maxStorageSizeBytes = value;
                HasMaxStorageSizeBytes = true;
            }
        }

        public int? MaxFileCount
        {
            get => _maxFileCount;
            set
            {
                _maxFileCount = value;
                HasMaxFileCount = true;
            }
        }

        [JsonIgnore]
        public bool HasMaxStorageSizeBytes { get; private set; }

        [JsonIgnore]
        public bool HasMaxFileCount { get; private set; }

        public MediaQuotaExceededBehavior? QuotaExceededBehavior { get; set; }
        public bool? IsActive { get; set; }
    }
}
