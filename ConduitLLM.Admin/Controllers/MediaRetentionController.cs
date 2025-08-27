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
        public async Task<IActionResult> TriggerCleanup(int groupId, [FromQuery] bool dryRun = true)
        {
            // This would trigger the media cleanup process
            // For now, return a placeholder response
            return Ok(new CleanupResultDto
            {
                VirtualKeyGroupId = groupId,
                DryRun = dryRun,
                MediaRecordsEvaluated = 0,
                MediaRecordsMarkedForDeletion = 0,
                MediaRecordsDeleted = 0,
                StorageBytesFreed = 0,
                Message = "Manual cleanup trigger not yet implemented. Use the scheduled cleanup system."
            });
        }
    }

    #region DTOs
    
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
        public bool IsProTier { get; set; }
        public bool IsDefault { get; set; }
        public long? MaxStorageSizeBytes { get; set; }
        public int? MaxFileCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int VirtualKeyGroupCount { get; set; }
    }

    public class MediaRetentionPolicyDetailDto : MediaRetentionPolicyDto
    {
        public List<VirtualKeyGroupSummaryDto> VirtualKeyGroups { get; set; } = new();
    }

    public class VirtualKeyGroupSummaryDto
    {
        public int Id { get; set; }
        public decimal Balance { get; set; }
        public int VirtualKeyCount { get; set; }
    }

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
        public bool IsProTier { get; set; }
        public bool IsDefault { get; set; }
        public long? MaxStorageSizeBytes { get; set; }
        public int? MaxFileCount { get; set; }
    }

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
        public bool? IsProTier { get; set; }
        public bool? IsDefault { get; set; }
        public long? MaxStorageSizeBytes { get; set; }
        public int? MaxFileCount { get; set; }
        public bool? IsActive { get; set; }
    }

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
    
    #endregion
}