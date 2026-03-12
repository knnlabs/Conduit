using ConduitLLM.Admin.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Admin controller for managing media retention policies.
    /// </summary>
    [ApiController]
    [Route("api/admin/media-retention")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class MediaRetentionController : AdminControllerBase
    {
        private readonly IConfigurationDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaRetentionController"/> class.
        /// </summary>
        /// <param name="context">The database context for configuration operations.</param>
        /// <param name="logger">The logger instance for diagnostic logging.</param>
        public MediaRetentionController(
            IConfigurationDbContext context,
            ILogger<MediaRetentionController> logger)
            : base(logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get all media retention policies.
        /// </summary>
        /// <returns>List of all retention policies</returns>
        [HttpGet("policies")]
        [ProducesResponseType(typeof(List<MediaRetentionPolicyDto>), 200)]
        public Task<IActionResult> GetPolicies()
        {
            return ExecuteAsync(
                async () => await _context.MediaRetentionPolicies
                    .Include(p => p.VirtualKeyGroups)
                    .OrderBy(p => p.Name)
                    .Select(p => new MediaRetentionPolicyDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        PositiveBalanceRetentionDays = p.PositiveBalanceRetentionDays,
                        ZeroBalanceRetentionDays = p.ZeroBalanceRetentionDays,
                        NegativeBalanceRetentionDays = p.NegativeBalanceRetentionDays,
                        SoftDeleteGracePeriodDays = p.SoftDeleteGracePeriodDays,
                        RespectRecentAccess = p.RespectRecentAccess,
                        RecentAccessWindowDays = p.RecentAccessWindowDays,
                        IsDefault = p.IsDefault,
                        MaxStorageSizeBytes = p.MaxStorageSizeBytes,
                        MaxFileCount = p.MaxFileCount,
                        IsActive = p.IsActive,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt,
                        VirtualKeyGroupCount = p.VirtualKeyGroups.Count
                    })
                    .ToListAsync(),
                policies => Ok(policies),
                nameof(GetPolicies));
        }

        /// <summary>
        /// Get a specific media retention policy by ID.
        /// </summary>
        /// <param name="id">Policy ID</param>
        /// <returns>The requested retention policy</returns>
        [HttpGet("policies/{id}")]
        [ProducesResponseType(typeof(MediaRetentionPolicyDetailDto), 200)]
        [ProducesResponseType(404)]
        public Task<IActionResult> GetPolicy(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _context.MediaRetentionPolicies
                    .Include(p => p.VirtualKeyGroups)
                        .ThenInclude(vkg => vkg.VirtualKeys)
                    .FirstOrDefaultAsync(p => p.Id == id),
                policy => Ok(new MediaRetentionPolicyDetailDto
                {
                    Id = policy.Id,
                    Name = policy.Name,
                    Description = policy.Description,
                    PositiveBalanceRetentionDays = policy.PositiveBalanceRetentionDays,
                    ZeroBalanceRetentionDays = policy.ZeroBalanceRetentionDays,
                    NegativeBalanceRetentionDays = policy.NegativeBalanceRetentionDays,
                    SoftDeleteGracePeriodDays = policy.SoftDeleteGracePeriodDays,
                    RespectRecentAccess = policy.RespectRecentAccess,
                    RecentAccessWindowDays = policy.RecentAccessWindowDays,
                    IsDefault = policy.IsDefault,
                    MaxStorageSizeBytes = policy.MaxStorageSizeBytes,
                    MaxFileCount = policy.MaxFileCount,
                    IsActive = policy.IsActive,
                    CreatedAt = policy.CreatedAt,
                    UpdatedAt = policy.UpdatedAt,
                    VirtualKeyGroups = policy.VirtualKeyGroups.Select(vkg => new VirtualKeyGroupSummaryDto
                    {
                        Id = vkg.Id,
                        Balance = vkg.Balance,
                        VirtualKeyCount = vkg.VirtualKeys.Count
                    }).ToList()
                }),
                "Retention policy", id, nameof(GetPolicy));
        }

        /// <summary>
        /// Create a new media retention policy.
        /// </summary>
        /// <param name="request">Policy creation request</param>
        /// <returns>The created retention policy</returns>
        [HttpPost("policies")]
        [ProducesResponseType(typeof(MediaRetentionPolicyDto), 201)]
        [ProducesResponseType(400)]
        public Task<IActionResult> CreatePolicy([FromBody] CreateMediaRetentionPolicyRequest request)
        {
            if (request.PositiveBalanceRetentionDays <= 0)
            {
                return Task.FromResult<IActionResult>(
                    this.BadRequestError("Positive balance retention days must be greater than 0"));
            }

            return ExecuteAsync<MediaRetentionPolicyDto>(
                async () =>
                {
                    if (request.IsDefault)
                    {
                        // Ensure only one default policy exists
                        var existingDefault = await _context.MediaRetentionPolicies
                            .FirstOrDefaultAsync(p => p.IsDefault);
                        if (existingDefault != null)
                        {
                            existingDefault.IsDefault = false;
                        }
                    }

                    var policy = new MediaRetentionPolicy
                    {
                        Name = request.Name,
                        Description = request.Description,
                        PositiveBalanceRetentionDays = request.PositiveBalanceRetentionDays,
                        ZeroBalanceRetentionDays = request.ZeroBalanceRetentionDays,
                        NegativeBalanceRetentionDays = request.NegativeBalanceRetentionDays,
                        SoftDeleteGracePeriodDays = request.SoftDeleteGracePeriodDays,
                        RespectRecentAccess = request.RespectRecentAccess,
                        RecentAccessWindowDays = request.RecentAccessWindowDays,
                        IsDefault = request.IsDefault,
                        MaxStorageSizeBytes = request.MaxStorageSizeBytes,
                        MaxFileCount = request.MaxFileCount,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.MediaRetentionPolicies.Add(policy);
                    await _context.SaveChangesAsync();

                    Logger.LogInformation("Created media retention policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);

                    return new MediaRetentionPolicyDto
                    {
                        Id = policy.Id,
                        Name = policy.Name,
                        Description = policy.Description,
                        PositiveBalanceRetentionDays = policy.PositiveBalanceRetentionDays,
                        ZeroBalanceRetentionDays = policy.ZeroBalanceRetentionDays,
                        NegativeBalanceRetentionDays = policy.NegativeBalanceRetentionDays,
                        SoftDeleteGracePeriodDays = policy.SoftDeleteGracePeriodDays,
                        RespectRecentAccess = policy.RespectRecentAccess,
                        RecentAccessWindowDays = policy.RecentAccessWindowDays,
                        IsDefault = policy.IsDefault,
                        MaxStorageSizeBytes = policy.MaxStorageSizeBytes,
                        MaxFileCount = policy.MaxFileCount,
                        IsActive = policy.IsActive,
                        CreatedAt = policy.CreatedAt,
                        UpdatedAt = policy.UpdatedAt,
                        VirtualKeyGroupCount = 0
                    };
                },
                dto => CreatedAtAction(nameof(GetPolicy), new { id = dto.Id }, dto),
                nameof(CreatePolicy));
        }

        /// <summary>
        /// Update an existing media retention policy.
        /// </summary>
        /// <param name="id">Policy ID</param>
        /// <param name="request">Policy update request</param>
        /// <returns>The updated retention policy</returns>
        [HttpPut("policies/{id}")]
        [ProducesResponseType(typeof(MediaRetentionPolicyDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public Task<IActionResult> UpdatePolicy(int id, [FromBody] UpdateMediaRetentionPolicyRequest request)
        {
            return ExecuteWithNotFoundAsync(
                () => _context.MediaRetentionPolicies
                    .Include(p => p.VirtualKeyGroups)
                    .FirstOrDefaultAsync(p => p.Id == id),
                async policy =>
                {
                    if (request.IsDefault == true && !policy.IsDefault)
                    {
                        // Ensure only one default policy exists
                        var existingDefault = await _context.MediaRetentionPolicies
                            .FirstOrDefaultAsync(p => p.IsDefault && p.Id != id);
                        if (existingDefault != null)
                        {
                            existingDefault.IsDefault = false;
                        }
                    }

                    // Update fields
                    policy.Name = request.Name ?? policy.Name;
                    policy.Description = request.Description ?? policy.Description;
                    policy.PositiveBalanceRetentionDays = request.PositiveBalanceRetentionDays ?? policy.PositiveBalanceRetentionDays;
                    policy.ZeroBalanceRetentionDays = request.ZeroBalanceRetentionDays ?? policy.ZeroBalanceRetentionDays;
                    policy.NegativeBalanceRetentionDays = request.NegativeBalanceRetentionDays ?? policy.NegativeBalanceRetentionDays;
                    policy.SoftDeleteGracePeriodDays = request.SoftDeleteGracePeriodDays ?? policy.SoftDeleteGracePeriodDays;
                    policy.RespectRecentAccess = request.RespectRecentAccess ?? policy.RespectRecentAccess;
                    policy.RecentAccessWindowDays = request.RecentAccessWindowDays ?? policy.RecentAccessWindowDays;
                    policy.IsDefault = request.IsDefault ?? policy.IsDefault;
                    policy.MaxStorageSizeBytes = request.MaxStorageSizeBytes ?? policy.MaxStorageSizeBytes;
                    policy.MaxFileCount = request.MaxFileCount ?? policy.MaxFileCount;
                    policy.IsActive = request.IsActive ?? policy.IsActive;
                    policy.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    Logger.LogInformation("Updated media retention policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);

                    return Ok(new MediaRetentionPolicyDto
                    {
                        Id = policy.Id,
                        Name = policy.Name,
                        Description = policy.Description,
                        PositiveBalanceRetentionDays = policy.PositiveBalanceRetentionDays,
                        ZeroBalanceRetentionDays = policy.ZeroBalanceRetentionDays,
                        NegativeBalanceRetentionDays = policy.NegativeBalanceRetentionDays,
                        SoftDeleteGracePeriodDays = policy.SoftDeleteGracePeriodDays,
                        RespectRecentAccess = policy.RespectRecentAccess,
                        RecentAccessWindowDays = policy.RecentAccessWindowDays,
                        IsDefault = policy.IsDefault,
                        MaxStorageSizeBytes = policy.MaxStorageSizeBytes,
                        MaxFileCount = policy.MaxFileCount,
                        IsActive = policy.IsActive,
                        CreatedAt = policy.CreatedAt,
                        UpdatedAt = policy.UpdatedAt,
                        VirtualKeyGroupCount = policy.VirtualKeyGroups.Count
                    });
                },
                "Retention policy", id, nameof(UpdatePolicy));
        }

        /// <summary>
        /// Delete a media retention policy.
        /// </summary>
        /// <param name="id">Policy ID</param>
        /// <returns>No content on success</returns>
        [HttpDelete("policies/{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(400)]
        public Task<IActionResult> DeletePolicy(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _context.MediaRetentionPolicies
                    .Include(p => p.VirtualKeyGroups)
                    .FirstOrDefaultAsync(p => p.Id == id),
                async policy =>
                {
                    if (policy.IsDefault)
                    {
                        return this.BadRequestError("Cannot delete the default retention policy");
                    }

                    if (policy.VirtualKeyGroups.Any())
                    {
                        return this.BadRequestError(
                            $"Cannot delete policy - it is assigned to {policy.VirtualKeyGroups.Count} virtual key group(s)");
                    }

                    _context.MediaRetentionPolicies.Remove(policy);
                    await _context.SaveChangesAsync();

                    Logger.LogInformation("Deleted media retention policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);

                    return NoContent();
                },
                "Retention policy", id, nameof(DeletePolicy));
        }

        /// <summary>
        /// Assign a retention policy to a virtual key group.
        /// </summary>
        /// <param name="groupId">Virtual key group ID</param>
        /// <param name="policyId">Retention policy ID</param>
        /// <returns>Success result</returns>
        [HttpPost("assign/{groupId}/{policyId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public Task<IActionResult> AssignPolicyToGroup(int groupId, int policyId)
        {
            return ExecuteAsync(async () =>
            {
                var group = await _context.VirtualKeyGroups.FindAsync(groupId);
                if (group == null)
                {
                    return this.NotFoundEntity("Virtual key group", groupId);
                }

                var policy = await _context.MediaRetentionPolicies.FindAsync(policyId);
                if (policy == null)
                {
                    return this.NotFoundEntity("Retention policy", policyId);
                }

                group.MediaRetentionPolicyId = policyId;
                await _context.SaveChangesAsync();

                Logger.LogInformation("Assigned retention policy {PolicyId} to virtual key group {GroupId}", policyId, groupId);

                return Ok(new { message = $"Successfully assigned policy '{policy.Name}' to group {groupId}" });
            }, nameof(AssignPolicyToGroup), new { groupId, policyId });
        }

        /// <summary>
        /// Sets a policy as the new default retention policy.
        /// Only one policy can be the default at a time.
        /// </summary>
        /// <param name="id">Policy ID to set as default</param>
        /// <returns>Success result</returns>
        [HttpPost("policies/{id}/set-default")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public Task<IActionResult> SetDefaultPolicy(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _context.MediaRetentionPolicies.FindAsync(id).AsTask(),
                async policy =>
                {
                    if (!policy.IsActive)
                    {
                        return this.BadRequestError("Cannot set an inactive policy as default");
                    }

                    // Clear existing default
                    var currentDefault = await _context.MediaRetentionPolicies
                        .FirstOrDefaultAsync(p => p.IsDefault && p.Id != id);
                    if (currentDefault != null)
                    {
                        currentDefault.IsDefault = false;
                    }

                    // Set new default
                    policy.IsDefault = true;
                    policy.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    Logger.LogInformation("Set retention policy {PolicyId} '{PolicyName}' as default", policy.Id, policy.Name);

                    return Ok(new { message = $"'{policy.Name}' is now the default retention policy" });
                },
                "Retention policy", id, nameof(SetDefaultPolicy));
        }

        /// <summary>
        /// Trigger a manual media cleanup for a specific virtual key group.
        /// </summary>
        /// <param name="groupId">Virtual key group ID</param>
        /// <param name="dryRun">Whether to perform a dry run (default: true)</param>
        /// <returns>Cleanup statistics</returns>
        [HttpPost("cleanup/{groupId}")]
        [ProducesResponseType(typeof(CleanupResultDto), 200)]
        [ProducesResponseType(404)]
        public Task<IActionResult> TriggerCleanup(int groupId, [FromQuery] bool dryRun = true)
        {
            // Placeholder — manual cleanup not yet implemented
            return Task.FromResult<IActionResult>(Ok(new CleanupResultDto
            {
                VirtualKeyGroupId = groupId,
                DryRun = dryRun,
                MediaRecordsEvaluated = 0,
                MediaRecordsMarkedForDeletion = 0,
                MediaRecordsDeleted = 0,
                StorageBytesFreed = 0,
                Message = "Manual cleanup trigger not yet implemented. Use the scheduled cleanup system."
            }));
        }
    }

    #region DTOs
    
    /// <summary>
    /// Data transfer object for media retention policy information.
    /// </summary>
    public class MediaRetentionPolicyDto
    {
        /// <summary>
        /// Gets or sets the unique identifier of the retention policy.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the retention policy.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the description of the retention policy.
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is positive.
        /// </summary>
        public int PositiveBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is zero.
        /// </summary>
        public int ZeroBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is negative.
        /// </summary>
        public int NegativeBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the grace period in days before permanently deleting soft-deleted media.
        /// </summary>
        public int SoftDeleteGracePeriodDays { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to respect recent access when determining retention.
        /// </summary>
        public bool RespectRecentAccess { get; set; }
        
        /// <summary>
        /// Gets or sets the window in days for considering recent access.
        /// </summary>
        public int RecentAccessWindowDays { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the default policy.
        /// </summary>
        public bool IsDefault { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum storage size in bytes allowed for this policy.
        /// </summary>
        public long? MaxStorageSizeBytes { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum number of files allowed for this policy.
        /// </summary>
        public int? MaxFileCount { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this policy is active.
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Gets or sets the date and time when the policy was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Gets or sets the date and time when the policy was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
        
        /// <summary>
        /// Gets or sets the count of virtual key groups using this policy.
        /// </summary>
        public int VirtualKeyGroupCount { get; set; }
    }

    /// <summary>
    /// Extended DTO for media retention policy with additional details.
    /// </summary>
    public class MediaRetentionPolicyDetailDto : MediaRetentionPolicyDto
    {
        /// <summary>
        /// Gets or sets the list of virtual key groups associated with this policy.
        /// </summary>
        public List<VirtualKeyGroupSummaryDto> VirtualKeyGroups { get; set; } = new();
    }

    /// <summary>
    /// Summary information for a virtual key group.
    /// </summary>
    public class VirtualKeyGroupSummaryDto
    {
        /// <summary>
        /// Gets or sets the virtual key group identifier.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the current balance of the virtual key group.
        /// </summary>
        public decimal Balance { get; set; }
        
        /// <summary>
        /// Gets or sets the count of virtual keys in the group.
        /// </summary>
        public int VirtualKeyCount { get; set; }
    }

    /// <summary>
    /// Request model for creating a new media retention policy.
    /// </summary>
    public class CreateMediaRetentionPolicyRequest
    {
        /// <summary>
        /// Gets or sets the name of the retention policy.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the description of the retention policy.
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is positive.
        /// </summary>
        public int PositiveBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is zero.
        /// </summary>
        public int ZeroBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is negative.
        /// </summary>
        public int NegativeBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the grace period in days before permanently deleting soft-deleted media.
        /// </summary>
        public int SoftDeleteGracePeriodDays { get; set; } = 7;
        
        /// <summary>
        /// Gets or sets a value indicating whether to respect recent access when determining retention.
        /// </summary>
        public bool RespectRecentAccess { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the window in days for considering recent access.
        /// </summary>
        public int RecentAccessWindowDays { get; set; } = 7;

        /// <summary>
        /// Gets or sets a value indicating whether this is the default policy.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets or sets the maximum storage size in bytes allowed for this policy.
        /// </summary>
        public long? MaxStorageSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of files allowed for this policy.
        /// </summary>
        public int? MaxFileCount { get; set; }
    }

    /// <summary>
    /// Request model for updating an existing media retention policy.
    /// </summary>
    public class UpdateMediaRetentionPolicyRequest
    {
        /// <summary>
        /// Gets or sets the name of the retention policy.
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of the retention policy.
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is positive.
        /// </summary>
        public int? PositiveBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is zero.
        /// </summary>
        public int? ZeroBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the retention period in days for media when balance is negative.
        /// </summary>
        public int? NegativeBalanceRetentionDays { get; set; }
        
        /// <summary>
        /// Gets or sets the grace period in days before permanently deleting soft-deleted media.
        /// </summary>
        public int? SoftDeleteGracePeriodDays { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to respect recent access when determining retention.
        /// </summary>
        public bool? RespectRecentAccess { get; set; }
        
        /// <summary>
        /// Gets or sets the window in days for considering recent access.
        /// </summary>
        public int? RecentAccessWindowDays { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the default policy.
        /// </summary>
        public bool? IsDefault { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum storage size in bytes allowed for this policy.
        /// </summary>
        public long? MaxStorageSizeBytes { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum number of files allowed for this policy.
        /// </summary>
        public int? MaxFileCount { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this policy is active.
        /// </summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Represents the result of a media cleanup operation.
    /// </summary>
    public class CleanupResultDto
    {
        /// <summary>
        /// Gets or sets the ID of the virtual key group that was cleaned up.
        /// </summary>
        public int VirtualKeyGroupId { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this was a dry run (no actual deletions).
        /// </summary>
        public bool DryRun { get; set; }
        
        /// <summary>
        /// Gets or sets the total number of media records evaluated during cleanup.
        /// </summary>
        public int MediaRecordsEvaluated { get; set; }
        
        /// <summary>
        /// Gets or sets the number of media records marked for deletion.
        /// </summary>
        public int MediaRecordsMarkedForDeletion { get; set; }
        
        /// <summary>
        /// Gets or sets the number of media records actually deleted.
        /// </summary>
        public int MediaRecordsDeleted { get; set; }
        
        /// <summary>
        /// Gets or sets the total amount of storage space freed in bytes.
        /// </summary>
        public long StorageBytesFreed { get; set; }
        
        /// <summary>
        /// Gets or sets an informational message about the cleanup operation.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
    
    #endregion
}