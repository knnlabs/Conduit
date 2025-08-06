using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Enums;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.DTOs.VirtualKey;

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
        private readonly ILogger<VirtualKeyGroupsController> _logger;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyGroupsController
        /// </summary>
        public VirtualKeyGroupsController(
            IVirtualKeyGroupRepository groupRepository,
            IVirtualKeyRepository keyRepository,
            IConfigurationDbContext context,
            ILogger<VirtualKeyGroupsController> logger)
        {
            _groupRepository = groupRepository;
            _keyRepository = keyRepository;
            _context = context;
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
                _logger.LogInformation("Repository returned {Count} groups", groups.Count);
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
                if (group.VirtualKeys?.Any() == true)
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

                // Get total count
                var totalCount = await _context.VirtualKeyGroupTransactions
                    .Where(t => t.VirtualKeyGroupId == id && !t.IsDeleted)
                    .CountAsync();

                // Calculate pagination
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                var skip = (page - 1) * pageSize;

                // Get paginated transactions
                var transactions = await _context.VirtualKeyGroupTransactions
                    .Where(t => t.VirtualKeyGroupId == id && !t.IsDeleted)
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
    }
}