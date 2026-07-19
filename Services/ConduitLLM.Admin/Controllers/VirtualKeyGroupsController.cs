using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Core.Models;
using ConduitLLM.Admin.Interfaces;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing virtual key groups
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class VirtualKeyGroupsController : AdminControllerBase
    {
        private readonly IVirtualKeyGroupRepository _groupRepository;
        private readonly IVirtualKeyRepository _keyRepository;
        private readonly IConfigurationDbContext _context;
        private readonly IRefundService _refundService;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyGroupsController
        /// </summary>
        public VirtualKeyGroupsController(
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeyRepository keyRepository,
            IConfigurationDbContext context,
            IRefundService refundService,
            ILogger<VirtualKeyGroupsController> logger)
            : base(logger)
        {
            _groupRepository = groupRepository;
            _keyRepository = keyRepository;
            _context = context;
            _refundService = refundService;
        }

        /// <summary>
        /// Get all virtual key groups with pagination
        /// </summary>
        /// <param name="page">Page number (1-based, default: 1)</param>
        /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<VirtualKeyGroupDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllGroups(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            // Validate and clamp page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var (groups, totalCount) = await _groupRepository.GetPaginatedAsync(page, pageSize, cancellationToken);

            var dtos = groups.Select(g => new VirtualKeyGroupDto
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
            }).ToList();

            return Ok(new PagedResult<VirtualKeyGroupDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Get a specific virtual key group by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(VirtualKeyGroupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGroup(int id)
        {
            var group = await _groupRepository.GetByIdWithKeysAsync(id);
            if (group == null)
            {
                return this.NotFoundEntity("VirtualKeyGroup", id);
            }
            return Ok(new VirtualKeyGroupDto
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
            });
        }

        /// <summary>
        /// Create a new virtual key group
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(VirtualKeyGroupDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateGroup([FromBody] CreateVirtualKeyGroupRequestDto request)
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

            LogAdminAudit("Created", "VirtualKeyGroup", id,
                $"Name: {group.GroupName}, InitialBalance: {group.Balance}");
            AdminOperationsMetricsService.RecordConfigurationChange("virtualkeygroup", "create");

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

        /// <summary>
        /// Update a virtual key group
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateGroup(int id, [FromBody] UpdateVirtualKeyGroupRequestDto request)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
                throw new KeyNotFoundException();

            var changes = new List<(string Property, string? OldValue, string? NewValue)>();

            if (!string.IsNullOrEmpty(request.GroupName))
            {
                changes.Add(("GroupName", group.GroupName, request.GroupName));
                group.GroupName = request.GroupName;
            }

            if (!string.IsNullOrEmpty(request.ExternalGroupId))
            {
                changes.Add(("ExternalGroupId", group.ExternalGroupId, request.ExternalGroupId));
                group.ExternalGroupId = request.ExternalGroupId;
            }

            await _groupRepository.UpdateAsync(group);

            LogAdminAuditWithChanges("VirtualKeyGroup", id, changes);
            AdminOperationsMetricsService.RecordConfigurationChange("virtualkeygroup", "update");

            return NoContent();
        }

        /// <summary>
        /// Adjust the balance of a virtual key group
        /// </summary>
        [HttpPost("{id}/adjust-balance")]
        [ProducesResponseType(typeof(VirtualKeyGroupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdjustBalance(int id, [FromBody] AdjustBalanceDto request)
        {
            // Get the authenticated user's identity
            var initiatedBy = User.Identity?.Name ?? "System";

            var newBalance = await _groupRepository.AdjustBalanceAsync(
                id,
                request.Amount,
                request.Description,
                initiatedBy
            );

            LogAdminAudit("AdjustedBalance", "VirtualKeyGroup", id,
                $"Amount: {request.Amount}, Description: {request.Description}, NewBalance: {newBalance}");

            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
                throw new KeyNotFoundException();

            return Ok(new VirtualKeyGroupDto
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
            });
        }

        /// <summary>
        /// Delete a virtual key group
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
                throw new KeyNotFoundException();

            // Check if group has any keys
            if (group.VirtualKeys?.Count > 0)
                throw new InvalidOperationException("Cannot delete group with existing virtual keys");

            await _groupRepository.DeleteAsync(id);

            LogAdminAudit("Deleted", "VirtualKeyGroup", id,
                $"Name: {group.GroupName}");
            AdminOperationsMetricsService.RecordConfigurationChange("virtualkeygroup", "delete");

            return NoContent();
        }

        /// <summary>
        /// Get transaction history for a virtual key group
        /// </summary>
        [HttpGet("{id}/transactions")]
        [ProducesResponseType(typeof(PagedResult<VirtualKeyGroupTransactionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransactionHistory(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            // Validate page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
                throw new KeyNotFoundException();

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

            return Ok(new PagedResult<VirtualKeyGroupTransactionDto>
            {
                Items = transactions,
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        /// <summary>
        /// Get virtual keys in a group
        /// </summary>
        [HttpGet("{id}/keys")]
        [ProducesResponseType(typeof(List<VirtualKeyDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetKeysInGroup(int id)
        {
            var group = await _groupRepository.GetByIdWithKeysAsync(id);
            if (group == null)
            {
                return this.NotFoundEntity("VirtualKeyGroup", id);
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
        public async Task<IActionResult> ProcessRefund(int id, [FromBody] ProcessRefundRequestDto request)
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

            if (string.IsNullOrWhiteSpace(request.OriginalTransactionId))
            {
                return BadRequest(new { message = "Original transaction ID is required" });
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
                initiatedByUserId,
                request.RequestLogId);

            // Get updated group info for balance
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null)
                throw new KeyNotFoundException();

            // Map to response DTO
            var responseDto = MapToRefundResultDto(refundResult, group.Balance);

            LogAdminAudit("Refunded", "VirtualKeyGroup", id,
                $"Amount: {refundResult.RefundAmount:C}, Model: {request.ModelId}, Reason: {request.RefundReason}, TransactionId: {refundResult.RefundTransactionId}, OriginalTransactionId: {refundResult.OriginalTransactionId ?? "none"}");

            return Ok(responseDto);
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
                CachedInputTokensIncludedInPrompt = dto.CachedInputTokensIncludedInPrompt,
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
                TransactionId = result.RefundTransactionId,
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
                CachedInputTokensIncludedInPrompt = usage.CachedInputTokensIncludedInPrompt,
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
