using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing virtual key groups
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class VirtualKeyGroupsController : ControllerBase
    {
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly IVirtualKeyRepository _keyRepository;
        private readonly IConfigurationDbContext _context;
        private readonly IRefundService _refundService;
        private readonly ILogger<VirtualKeyGroupsController> _logger;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyGroupsController
        /// </summary>
        public VirtualKeyGroupsController(
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeyRepository keyRepository,
            IConfigurationDbContext context,
            IRefundService refundService,
            ILogger<VirtualKeyGroupsController> logger)
        {
            _groupRepository = groupRepository;
            _keyRepository = keyRepository;
            _context = context;
            _refundService = refundService;
            _logger = logger;
        }

        /// <summary>
        /// Get all virtual key groups
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<VirtualKeyGroupDto>>> GetAllGroups()
        {
            try
            {
                _logger.LogInformation("GetAllGroups called");
                var groups = await _groupRepository.GetAllAsync();
                _logger.LogInformation("Repository returned {Count} groups", groups.Count());
                var dtos = groups.Select(g => 
                {
                    _logger.LogInformation("Group {GroupId} has {KeyCount} keys (null: {IsNull})", 
                        g.Id, g.VirtualKeys?.Count ?? -1, g.VirtualKeys == null);
                    
                    return new VirtualKeyGroupDto
                    {
                        Id = g.Id,
                        ExternalGroupId = g.ExternalGroupId,
                        GroupName = g.GroupName,
                        Balance = g.Balance,
                        LifetimeCreditsAdded = g.LifetimeCreditsAdded,
                        LifetimeSpent = g.LifetimeSpent,
                        CreatedAt = g.CreatedAt,
                        UpdatedAt = g.UpdatedAt,
                        VirtualKeyCount = g.VirtualKeys?.Count ?? 0
                    };
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key groups");
                return StatusCode(500, new { message = "An error occurred while retrieving groups" });
            }
        }

        /// <summary>
        /// Get a specific virtual key group by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<VirtualKeyGroupDto>> GetGroup(int id)
        {
            try
            {
                var group = await _groupRepository.GetByIdWithKeysAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                var dto = new VirtualKeyGroupDto
                {
                    Id = group.Id,
                    ExternalGroupId = group.ExternalGroupId,
                    GroupName = group.GroupName,
                    Balance = group.Balance,
                    LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                    LifetimeSpent = group.LifetimeSpent,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt,
                    VirtualKeyCount = group.VirtualKeys?.Count ?? 0
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the group" });
            }
        }

        /// <summary>
        /// Create a new virtual key group
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<VirtualKeyGroupDto>> CreateGroup([FromBody] CreateVirtualKeyGroupRequestDto request)
        {
            try
            {
                var group = new VirtualKeyGroup
                {
                    ExternalGroupId = request.ExternalGroupId,
                    GroupName = request.GroupName,
                    Balance = request.InitialBalance ?? 0,
                    LifetimeCreditsAdded = request.InitialBalance ?? 0,
                    LifetimeSpent = 0
                };

                var id = await _groupRepository.CreateAsync(group);
                group.Id = id;

                var dto = new VirtualKeyGroupDto
                {
                    Id = group.Id,
                    ExternalGroupId = group.ExternalGroupId,
                    GroupName = group.GroupName,
                    Balance = group.Balance,
                    LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                    LifetimeSpent = group.LifetimeSpent,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt,
                    VirtualKeyCount = 0
                };

                return CreatedAtAction(nameof(GetGroup), new { id = group.Id }, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating virtual key group");
                return StatusCode(500, new { message = "An error occurred while creating the group" });
            }
        }

        /// <summary>
        /// Update a virtual key group
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateGroup(int id, [FromBody] UpdateVirtualKeyGroupRequestDto request)
        {
            try
            {
                var group = await _groupRepository.GetByIdAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                if (!string.IsNullOrEmpty(request.GroupName))
                {
                    group.GroupName = request.GroupName;
                }

                if (!string.IsNullOrEmpty(request.ExternalGroupId))
                {
                    group.ExternalGroupId = request.ExternalGroupId;
                }

                await _groupRepository.UpdateAsync(group);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the group" });
            }
        }

        /// <summary>
        /// Adjust the balance of a virtual key group
        /// </summary>
        [HttpPost("{id}/adjust-balance")]
        public async Task<ActionResult<VirtualKeyGroupDto>> AdjustBalance(int id, [FromBody] AdjustBalanceDto request)
        {
            try
            {
                // Get the authenticated user's identity
                var initiatedBy = User.Identity?.Name ?? "System";
                
                var newBalance = await _groupRepository.AdjustBalanceAsync(
                    id, 
                    request.Amount,
                    request.Description,
                    initiatedBy
                );
                
                var group = await _groupRepository.GetByIdAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                var dto = new VirtualKeyGroupDto
                {
                    Id = group.Id,
                    ExternalGroupId = group.ExternalGroupId,
                    GroupName = group.GroupName,
                    Balance = group.Balance,
                    LifetimeCreditsAdded = group.LifetimeCreditsAdded,
                    LifetimeSpent = group.LifetimeSpent,
                    CreatedAt = group.CreatedAt,
                    UpdatedAt = group.UpdatedAt,
                    VirtualKeyCount = group.VirtualKeys?.Count ?? 0
                };

                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adjusting balance for virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while adjusting the balance" });
            }
        }

        /// <summary>
        /// Delete a virtual key group
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteGroup(int id)
        {
            try
            {
                var group = await _groupRepository.GetByIdAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                // Check if group has any keys
                if (group.VirtualKeys?.Count > 0)
                {
                    return BadRequest(new { message = "Cannot delete group with existing virtual keys" });
                }

                await _groupRepository.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the group" });
            }
        }

        /// <summary>
        /// Get transaction history for a virtual key group
        /// </summary>
        [HttpGet("{id}/transactions")]
        [ProducesResponseType(typeof(PagedResult<VirtualKeyGroupTransactionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PagedResult<VirtualKeyGroupTransactionDto>>> GetTransactionHistory(
            int id, 
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var group = await _groupRepository.GetByIdAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                // Validate page parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 100) pageSize = 100;

                // Get total count (soft delete filter applied automatically via named query filter)
                var totalCount = await _context.VirtualKeyGroupTransactions
                    .Where(t => t.VirtualKeyGroupId == id)
                    .CountAsync();

                // Calculate pagination
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                var skip = (page - 1) * pageSize;

                // Get paginated transactions (soft delete filter applied automatically via named query filter)
                var transactions = await _context.VirtualKeyGroupTransactions
                    .Where(t => t.VirtualKeyGroupId == id)
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(t => new VirtualKeyGroupTransactionDto
                    {
                        Id = t.Id,
                        VirtualKeyGroupId = t.VirtualKeyGroupId,
                        TransactionType = t.TransactionType,
                        Amount = t.Amount,
                        BalanceAfter = t.BalanceAfter,
                        Description = t.Description,
                        ReferenceId = t.ReferenceId,
                        ReferenceType = t.ReferenceType,
                        InitiatedBy = t.InitiatedBy,
                        InitiatedByUserId = t.InitiatedByUserId,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync();

                var result = new PagedResult<VirtualKeyGroupTransactionDto>
                {
                    Items = transactions,
                    TotalCount = totalCount,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transaction history for virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the transaction history" });
            }
        }

        /// <summary>
        /// Get virtual keys in a group
        /// </summary>
        [HttpGet("{id}/keys")]
        public async Task<ActionResult<List<VirtualKeyDto>>> GetKeysInGroup(int id)
        {
            try
            {
                var group = await _groupRepository.GetByIdWithKeysAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                var keys = group.VirtualKeys?.Select(k => new VirtualKeyDto
                {
                    Id = k.Id,
                    KeyName = k.KeyName,
                    KeyPrefix = k.KeyHash?.Length > 10 ? k.KeyHash.Substring(0, 10) + "..." : k.KeyHash,
                    AllowedModels = k.AllowedModels,
                    VirtualKeyGroupId = k.VirtualKeyGroupId,
                    IsEnabled = k.IsEnabled,
                    ExpiresAt = k.ExpiresAt,
                    CreatedAt = k.CreatedAt,
                    UpdatedAt = k.UpdatedAt,
                    Metadata = k.Metadata,
                    RateLimitRpm = k.RateLimitRpm,
                    RateLimitRpd = k.RateLimitRpd,
                    Description = k.Description
                }).ToList() ?? new List<VirtualKeyDto>();

                return Ok(keys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving keys for virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the keys" });
            }
        }

        /// <summary>
        /// Process a refund for a virtual key group
        /// </summary>
        /// <param name="id">The virtual key group ID</param>
        /// <param name="request">The refund request details</param>
        /// <returns>The refund result with transaction details</returns>
        [HttpPost("{id}/refund")]
        [ProducesResponseType(typeof(RefundResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RefundResultDto>> ProcessRefund(int id, [FromBody] ProcessRefundRequestDto request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.ModelId))
                {
                    return BadRequest(new { message = "Model ID is required" });
                }

                if (string.IsNullOrEmpty(request.RefundReason))
                {
                    return BadRequest(new { message = "Refund reason is required" });
                }

                // Get user info for audit trail
                var initiatedBy = User.Identity?.Name ?? "System";
                var initiatedByUserId = User.FindFirst("sub")?.Value; // Clerk user ID from JWT

                // Convert DTOs to core models
                var originalUsage = MapToUsage(request.OriginalUsage);
                var refundUsage = MapToUsage(request.RefundUsage);

                // Process the refund
                var refundResult = await _refundService.ProcessRefundAsync(
                    id,
                    request.ModelId,
                    originalUsage,
                    refundUsage,
                    request.RefundReason,
                    request.OriginalTransactionId,
                    initiatedBy,
                    initiatedByUserId);

                // Get updated group info for balance
                var group = await _groupRepository.GetByIdAsync(id);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                // Map to response DTO
                var responseDto = MapToRefundResultDto(refundResult, group.Balance);

                _logger.LogInformation(
                    "Refund processed for group {GroupId}: {RefundAmount:C}, Transaction ID: {TransactionId}",
                    id,
                    refundResult.RefundAmount,
                    refundResult.OriginalTransactionId);

                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while processing refund for group {GroupId}", id);
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid refund request for group {GroupId}", id);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for virtual key group {GroupId}", id);
                return StatusCode(500, new { message = "An error occurred while processing the refund" });
            }
        }

        /// <summary>
        /// Maps UsageDto to Usage core model
        /// </summary>
        private static Usage MapToUsage(UsageDto dto)
        {
            return new Usage
            {
                PromptTokens = dto.PromptTokens,
                CompletionTokens = dto.CompletionTokens,
                TotalTokens = dto.TotalTokens,
                CachedInputTokens = dto.CachedInputTokens,
                CachedWriteTokens = dto.CachedWriteTokens,
                ReasoningTokens = dto.ReasoningTokens,
                ImageCount = dto.ImageCount,
                ImageQuality = dto.ImageQuality,
                ImageResolution = dto.ImageResolution,
                VideoDurationSeconds = dto.VideoDurationSeconds,
                VideoResolution = dto.VideoResolution,
                SearchUnits = dto.SearchUnits,
                InferenceSteps = dto.InferenceSteps,
                IsBatch = dto.IsBatch
            };
        }

        /// <summary>
        /// Maps RefundResult core model to RefundResultDto
        /// </summary>
        private static RefundResultDto MapToRefundResultDto(RefundResult result, decimal balanceAfter)
        {
            return new RefundResultDto
            {
                TransactionId = long.Parse(result.OriginalTransactionId ?? "0"),
                ModelId = result.ModelId,
                OriginalUsage = MapToUsageDto(result.OriginalUsage),
                RefundUsage = MapToUsageDto(result.RefundUsage),
                RefundAmount = result.RefundAmount,
                BalanceAfter = balanceAfter,
                OriginalTransactionId = result.OriginalTransactionId,
                RefundReason = result.RefundReason,
                RefundedAt = result.RefundedAt,
                IsPartialRefund = result.IsPartialRefund,
                ValidationMessages = result.ValidationMessages,
                Breakdown = result.Breakdown != null ? new RefundBreakdownDto
                {
                    InputTokenRefund = result.Breakdown.InputTokenRefund,
                    OutputTokenRefund = result.Breakdown.OutputTokenRefund,
                    ImageRefund = result.Breakdown.ImageRefund,
                    VideoRefund = result.Breakdown.VideoRefund,
                    EmbeddingRefund = result.Breakdown.EmbeddingRefund,
                    SearchUnitRefund = result.Breakdown.SearchUnitRefund,
                    InferenceStepRefund = result.Breakdown.InferenceStepRefund
                } : null
            };
        }

        /// <summary>
        /// Maps Usage core model to UsageDto
        /// </summary>
        private static UsageDto MapToUsageDto(Usage usage)
        {
            return new UsageDto
            {
                PromptTokens = usage.PromptTokens,
                CompletionTokens = usage.CompletionTokens,
                TotalTokens = usage.TotalTokens,
                CachedInputTokens = usage.CachedInputTokens,
                CachedWriteTokens = usage.CachedWriteTokens,
                ReasoningTokens = usage.ReasoningTokens,
                ImageCount = usage.ImageCount,
                ImageQuality = usage.ImageQuality,
                ImageResolution = usage.ImageResolution,
                VideoDurationSeconds = usage.VideoDurationSeconds,
                VideoResolution = usage.VideoResolution,
                SearchUnits = usage.SearchUnits,
                InferenceSteps = usage.InferenceSteps,
                IsBatch = usage.IsBatch
            };
        }
    }
}
