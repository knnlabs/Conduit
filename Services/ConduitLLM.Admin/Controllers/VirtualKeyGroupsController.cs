using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetAllGroups(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            // Validate and clamp page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            return ExecuteAsync(
                async () =>
                {
                    Logger.LogInformation("GetAllGroups called with page={Page}, pageSize={PageSize}", page, pageSize);

                    var (groups, totalCount) = await _groupRepository.GetPaginatedAsync(page, pageSize, cancellationToken);

                    Logger.LogInformation("Repository returned {Count} groups out of {TotalCount} total", groups.Count, totalCount);

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

                    return (object)new PagedResult<VirtualKeyGroupDto>
                    {
                        Items = dtos,
                        TotalCount = totalCount,
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                    };
                },
                Ok,
                "GetAllGroups");
        }

        /// <summary>
        /// Get a specific virtual key group by ID
        /// </summary>
        [HttpGet("{id}")]
        public Task<IActionResult> GetGroup(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _groupRepository.GetByIdWithKeysAsync(id),
                group => Ok(new VirtualKeyGroupDto
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
                }),
                "VirtualKeyGroup",
                id,
                "GetGroup");
        }

        /// <summary>
        /// Create a new virtual key group
        /// </summary>
        [HttpPost]
        public Task<IActionResult> CreateGroup([FromBody] CreateVirtualKeyGroupRequestDto request)
        {
            return ExecuteAsync(
                async () =>
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

                    return (IActionResult)CreatedAtAction(nameof(GetGroup), new { id = group.Id }, dto);
                },
                r => r,
                "CreateGroup");
        }

        /// <summary>
        /// Update a virtual key group
        /// </summary>
        [HttpPut("{id}")]
        public Task<IActionResult> UpdateGroup(int id, [FromBody] UpdateVirtualKeyGroupRequestDto request)
        {
            return ExecuteAsync(
                async () =>
                {
                    var group = await _groupRepository.GetByIdAsync(id);
                    if (group == null)
                        throw new KeyNotFoundException();

                    if (!string.IsNullOrEmpty(request.GroupName))
                    {
                        group.GroupName = request.GroupName;
                    }

                    if (!string.IsNullOrEmpty(request.ExternalGroupId))
                    {
                        group.ExternalGroupId = request.ExternalGroupId;
                    }

                    await _groupRepository.UpdateAsync(group);
                },
                NoContent(),
                "UpdateGroup",
                new { Id = id });
        }

        /// <summary>
        /// Adjust the balance of a virtual key group
        /// </summary>
        [HttpPost("{id}/adjust-balance")]
        public Task<IActionResult> AdjustBalance(int id, [FromBody] AdjustBalanceDto request)
        {
            return ExecuteAsync(
                async () =>
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
                        throw new KeyNotFoundException();

                    return (object)new VirtualKeyGroupDto
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
                },
                Ok,
                "AdjustBalance",
                new { Id = id });
        }

        /// <summary>
        /// Delete a virtual key group
        /// </summary>
        [HttpDelete("{id}")]
        public Task<IActionResult> DeleteGroup(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    var group = await _groupRepository.GetByIdAsync(id);
                    if (group == null)
                        throw new KeyNotFoundException();

                    // Check if group has any keys
                    if (group.VirtualKeys?.Count > 0)
                        throw new InvalidOperationException("Cannot delete group with existing virtual keys");

                    await _groupRepository.DeleteAsync(id);
                },
                NoContent(),
                "DeleteGroup",
                new { Id = id });
        }

        /// <summary>
        /// Get transaction history for a virtual key group
        /// </summary>
        [HttpGet("{id}/transactions")]
        [ProducesResponseType(typeof(PagedResult<VirtualKeyGroupTransactionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetTransactionHistory(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            // Validate page parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            return ExecuteAsync(
                async () =>
                {
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

                    return (object)new PagedResult<VirtualKeyGroupTransactionDto>
                    {
                        Items = transactions,
                        TotalCount = totalCount,
                        CurrentPage = page,
                        PageSize = pageSize,
                        TotalPages = totalPages
                    };
                },
                Ok,
                "GetTransactionHistory",
                new { Id = id });
        }

        /// <summary>
        /// Get virtual keys in a group
        /// </summary>
        [HttpGet("{id}/keys")]
        public Task<IActionResult> GetKeysInGroup(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _groupRepository.GetByIdWithKeysAsync(id),
                group =>
                {
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
                },
                "VirtualKeyGroup",
                id,
                "GetKeysInGroup");
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
        public Task<IActionResult> ProcessRefund(int id, [FromBody] ProcessRefundRequestDto request)
        {
            // Validate request
            if (string.IsNullOrEmpty(request.ModelId))
            {
                return Task.FromResult<IActionResult>(BadRequest(new { message = "Model ID is required" }));
            }

            if (string.IsNullOrEmpty(request.RefundReason))
            {
                return Task.FromResult<IActionResult>(BadRequest(new { message = "Refund reason is required" }));
            }

            return ExecuteAsync(
                async () =>
                {
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
                        throw new KeyNotFoundException();

                    // Map to response DTO
                    var responseDto = MapToRefundResultDto(refundResult, group.Balance);

                    Logger.LogInformation(
                        "Refund processed for group {GroupId}: {RefundAmount:C}, Transaction ID: {TransactionId}",
                        id,
                        refundResult.RefundAmount,
                        refundResult.OriginalTransactionId);

                    return (object)responseDto;
                },
                Ok,
                "ProcessRefund",
                new { Id = id });
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
