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
    [Authorize(Policy = "RequireAdminKey")]
    public class MediaRetentionController : ControllerBase
    {
        private readonly IConfigurationDbContext _context;
        private readonly ILogger<MediaRetentionController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaRetentionController"/> class.
        /// </summary>
        /// <param name="context">The database context for configuration operations.</param>
        /// <param name="logger">The logger instance for diagnostic logging.</param>
        public MediaRetentionController(
            IConfigurationDbContext context,
            ILogger<MediaRetentionController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all media retention policies.
        /// </summary>
        /// <returns>List of all retention policies</returns>
        [HttpGet("policies")]
        [ProducesResponseType(typeof(List<MediaRetentionPolicyDto>), 200)]
        public async Task<IActionResult> GetPolicies()
        {
            var policies = await _context.MediaRetentionPolicies
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
                    IsProTier = p.IsProTier,
                    IsDefault = p.IsDefault,
                    MaxStorageSizeBytes = p.MaxStorageSizeBytes,
                    MaxFileCount = p.MaxFileCount,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    VirtualKeyGroupCount = p.VirtualKeyGroups.Count
                })
                .ToListAsync();

            return Ok(policies);
        }

        /// <summary>
        /// Get a specific media retention policy by ID.
        /// </summary>
        /// <param name="id">Policy ID</param>
        /// <returns>The requested retention policy</returns>
        [HttpGet("policies/{id}")]
        [ProducesResponseType(typeof(MediaRetentionPolicyDetailDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetPolicy(int id)
        {
            var policy = await _context.MediaRetentionPolicies
                .Include(p => p.VirtualKeyGroups)
                    .ThenInclude(vkg => vkg.VirtualKeys)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (policy == null)
            {
                return NotFound(new { message = $"Policy with ID {id} not found" });
            }

            var dto = new MediaRetentionPolicyDetailDto
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
                IsProTier = policy.IsProTier,
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
            };

            return Ok(dto);
        }

        /// <summary>
        /// Create a new media retention policy.
        /// </summary>
        /// <param name="request">Policy creation request</param>
        /// <returns>The created retention policy</returns>
        [HttpPost("policies")]
        [ProducesResponseType(typeof(MediaRetentionPolicyDto), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreatePolicy([FromBody] CreateMediaRetentionPolicyRequest request)
        {
            // Validate request
            if (request.PositiveBalanceRetentionDays <= 0)
            {
                return BadRequest(new { message = "Positive balance retention days must be greater than 0" });
            }

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
                IsProTier = request.IsProTier,
                IsDefault = request.IsDefault,
                MaxStorageSizeBytes = request.MaxStorageSizeBytes,
                MaxFileCount = request.MaxFileCount,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.MediaRetentionPolicies.Add(policy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created media retention policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);

            return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, new MediaRetentionPolicyDto
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
                IsProTier = policy.IsProTier,
                IsDefault = policy.IsDefault,
                MaxStorageSizeBytes = policy.MaxStorageSizeBytes,
                MaxFileCount = policy.MaxFileCount,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt,
                UpdatedAt = policy.UpdatedAt,
                VirtualKeyGroupCount = 0
            });
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
        public async Task<IActionResult> UpdatePolicy(int id, [FromBody] UpdateMediaRetentionPolicyRequest request)
        {
            var policy = await _context.MediaRetentionPolicies
                .Include(p => p.VirtualKeyGroups)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (policy == null)
            {
                return NotFound(new { message = $"Policy with ID {id} not found" });
            }

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
            policy.IsProTier = request.IsProTier ?? policy.IsProTier;
            policy.IsDefault = request.IsDefault ?? policy.IsDefault;
            policy.MaxStorageSizeBytes = request.MaxStorageSizeBytes ?? policy.MaxStorageSizeBytes;
            policy.MaxFileCount = request.MaxFileCount ?? policy.MaxFileCount;
            policy.IsActive = request.IsActive ?? policy.IsActive;
            policy.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated media retention policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);

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
                IsProTier = policy.IsProTier,
                IsDefault = policy.IsDefault,
                MaxStorageSizeBytes = policy.MaxStorageSizeBytes,
                MaxFileCount = policy.MaxFileCount,
                IsActive = policy.IsActive,
                CreatedAt = policy.CreatedAt,
                UpdatedAt = policy.UpdatedAt,
                VirtualKeyGroupCount = policy.VirtualKeyGroups.Count
            });
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
        public async Task<IActionResult> DeletePolicy(int id)
        {
            var policy = await _context.MediaRetentionPolicies
                .Include(p => p.VirtualKeyGroups)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (policy == null)
            {
                return NotFound(new { message = $"Policy with ID {id} not found" });
            }

            if (policy.IsDefault)
            {
                return BadRequest(new { message = "Cannot delete the default retention policy" });
            }

            if (policy.VirtualKeyGroups.Any())
            {
                return BadRequest(new { message = $"Cannot delete policy - it is assigned to {policy.VirtualKeyGroups.Count} virtual key group(s)" });
            }

            _context.MediaRetentionPolicies.Remove(policy);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted media retention policy {PolicyId}: {PolicyName}", policy.Id, policy.Name);

            return NoContent();
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
        public async Task<IActionResult> AssignPolicyToGroup(int groupId, int policyId)
        {
            var group = await _context.VirtualKeyGroups.FindAsync(groupId);
            if (group == null)
            {
                return NotFound(new { message = $"Virtual key group with ID {groupId} not found" });
            }

            var policy = await _context.MediaRetentionPolicies.FindAsync(policyId);
            if (policy == null)
            {
                return NotFound(new { message = $"Retention policy with ID {policyId} not found" });
            }

            group.MediaRetentionPolicyId = policyId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned retention policy {PolicyId} to virtual key group {GroupId}", policyId, groupId);

            return Ok(new { message = $"Successfully assigned policy '{policy.Name}' to group {groupId}" });
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
            // This would trigger the media cleanup process
            // For now, return a placeholder response
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
        /// Gets or sets a value indicating whether this is a pro tier policy.
        /// </summary>
        public bool IsProTier { get; set; }
        
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
        /// Gets or sets a value indicating whether this is a pro tier policy.
        /// </summary>
        public bool IsProTier { get; set; }
        
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
        /// Gets or sets a value indicating whether this is a pro tier policy.
        /// </summary>
        public bool? IsProTier { get; set; }
        
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