using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents the budget duration for a virtual key.
/// </summary>
public enum BudgetDuration
{
    /// <summary>
    /// Total budget with no reset.
    /// </summary>
    Total,

    /// <summary>
    /// Daily budget that resets every day.
    /// </summary>
    Daily,

    /// <summary>
    /// Weekly budget that resets every week.
    /// </summary>
    Weekly,

    /// <summary>
    /// Monthly budget that resets every month.
    /// </summary>
    Monthly
}

/// <summary>
/// Represents a virtual key in the system.
/// </summary>
public class VirtualKeyDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the virtual key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name for the virtual key.
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key value (may be null for security reasons).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the key prefix for identification.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the allowed models for this key (comma-separated).
    /// </summary>
    public string AllowedModels { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum budget allowed.
    /// </summary>
    public decimal MaxBudget { get; set; }

    /// <summary>
    /// Gets or sets the current amount spent.
    /// </summary>
    public decimal CurrentSpend { get; set; }

    /// <summary>
    /// Gets or sets the budget duration type.
    /// </summary>
    public BudgetDuration BudgetDuration { get; set; }

    /// <summary>
    /// Gets or sets the budget start date.
    /// </summary>
    public DateTime BudgetStartDate { get; set; }

    /// <summary>
    /// Gets or sets whether the key is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the expiration date (optional).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the key was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets additional metadata (JSON string).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per minute.
    /// </summary>
    public int? RateLimitRpm { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per day.
    /// </summary>
    public int? RateLimitRpd { get; set; }

    /// <summary>
    /// Gets or sets when the key was last used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Gets or sets the total request count.
    /// </summary>
    public int? RequestCount { get; set; }
}

/// <summary>
/// Represents a request to create a new virtual key.
/// </summary>
public class CreateVirtualKeyRequest
{
    /// <summary>
    /// Gets or sets the display name for the virtual key.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the allowed models for this key (comma-separated).
    /// </summary>
    public string? AllowedModels { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget allowed.
    /// </summary>
    [Range(0, 1000000)]
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// Gets or sets the budget duration type.
    /// </summary>
    public BudgetDuration? BudgetDuration { get; set; }

    /// <summary>
    /// Gets or sets the expiration date (optional).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets additional metadata (JSON string).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per minute.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? RateLimitRpm { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per day.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? RateLimitRpd { get; set; }
}

/// <summary>
/// Represents the response when creating a virtual key.
/// </summary>
public class CreateVirtualKeyResponse
{
    /// <summary>
    /// Gets or sets the generated virtual key string.
    /// </summary>
    public string VirtualKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key information.
    /// </summary>
    public VirtualKeyDto KeyInfo { get; set; } = new();
}

/// <summary>
/// Represents a request to update an existing virtual key.
/// </summary>
public class UpdateVirtualKeyRequest
{
    /// <summary>
    /// Gets or sets the display name for the virtual key.
    /// </summary>
    public string? KeyName { get; set; }

    /// <summary>
    /// Gets or sets the allowed models for this key (comma-separated).
    /// </summary>
    public string? AllowedModels { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget allowed.
    /// </summary>
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// Gets or sets the budget duration type.
    /// </summary>
    public BudgetDuration? BudgetDuration { get; set; }

    /// <summary>
    /// Gets or sets whether the key is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the expiration date (optional).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets additional metadata (JSON string).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per minute.
    /// </summary>
    public int? RateLimitRpm { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per day.
    /// </summary>
    public int? RateLimitRpd { get; set; }
}

/// <summary>
/// Represents a request to validate a virtual key.
/// </summary>
public class VirtualKeyValidationRequest
{
    /// <summary>
    /// Gets or sets the key to validate.
    /// </summary>
    [Required]
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of virtual key validation.
/// </summary>
public class VirtualKeyValidationResult
{
    /// <summary>
    /// Gets or sets whether the key is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the virtual key ID if valid.
    /// </summary>
    public int? VirtualKeyId { get; set; }

    /// <summary>
    /// Gets or sets the key name if valid.
    /// </summary>
    public string? KeyName { get; set; }

    /// <summary>
    /// Gets or sets the reason for validation failure.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the allowed models for this key.
    /// </summary>
    public IEnumerable<string>? AllowedModels { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget.
    /// </summary>
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// Gets or sets the current spend amount.
    /// </summary>
    public decimal? CurrentSpend { get; set; }

    /// <summary>
    /// Gets or sets the remaining budget.
    /// </summary>
    public decimal? BudgetRemaining { get; set; }

    /// <summary>
    /// Gets or sets the expiration date.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per minute.
    /// </summary>
    public int? RateLimitRpm { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per day.
    /// </summary>
    public int? RateLimitRpd { get; set; }
}

/// <summary>
/// Represents a request to update spending for a virtual key.
/// </summary>
public class UpdateSpendRequest
{
    /// <summary>
    /// Gets or sets the amount to add to the current spend.
    /// </summary>
    [Required]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets an optional description of the spending.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Represents a request to check budget availability.
/// </summary>
public class CheckBudgetRequest
{
    /// <summary>
    /// Gets or sets the estimated cost to check against the budget.
    /// </summary>
    [Required]
    public decimal EstimatedCost { get; set; }
}

/// <summary>
/// Represents the response for budget availability check.
/// </summary>
public class CheckBudgetResponse
{
    /// <summary>
    /// Gets or sets whether there is available budget for the estimated cost.
    /// </summary>
    public bool HasAvailableBudget { get; set; }

    /// <summary>
    /// Gets or sets the available budget amount.
    /// </summary>
    public decimal AvailableBudget { get; set; }

    /// <summary>
    /// Gets or sets the estimated cost that was checked.
    /// </summary>
    public decimal EstimatedCost { get; set; }

    /// <summary>
    /// Gets or sets the current spend amount.
    /// </summary>
    public decimal CurrentSpend { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget.
    /// </summary>
    public decimal MaxBudget { get; set; }
}

/// <summary>
/// Represents detailed validation information for a virtual key.
/// </summary>
public class VirtualKeyValidationInfo
{
    /// <summary>
    /// Gets or sets the key ID.
    /// </summary>
    public int KeyId { get; set; }

    /// <summary>
    /// Gets or sets the key name.
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the key is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets any validation errors.
    /// </summary>
    public IEnumerable<string> ValidationErrors { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the allowed models for this key.
    /// </summary>
    public IEnumerable<string> AllowedModels { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the budget information.
    /// </summary>
    public BudgetInfo BudgetInfo { get; set; } = new();

    /// <summary>
    /// Gets or sets the rate limit information.
    /// </summary>
    public RateLimits? RateLimits { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents budget information for a virtual key.
/// </summary>
public class BudgetInfo
{
    /// <summary>
    /// Gets or sets the maximum budget.
    /// </summary>
    public decimal MaxBudget { get; set; }

    /// <summary>
    /// Gets or sets the current spend amount.
    /// </summary>
    public decimal CurrentSpend { get; set; }

    /// <summary>
    /// Gets or sets the remaining budget.
    /// </summary>
    public decimal Remaining { get; set; }

    /// <summary>
    /// Gets or sets the budget duration.
    /// </summary>
    public BudgetDuration Duration { get; set; }
}

/// <summary>
/// Represents rate limit information for a virtual key.
/// </summary>
public class RateLimits
{
    /// <summary>
    /// Gets or sets the rate limit per minute.
    /// </summary>
    public int? Rpm { get; set; }

    /// <summary>
    /// Gets or sets the rate limit per day.
    /// </summary>
    public int? Rpd { get; set; }
}

/// <summary>
/// Represents a request for virtual key maintenance operations.
/// </summary>
public class VirtualKeyMaintenanceRequest
{
    /// <summary>
    /// Gets or sets whether to clean up expired keys.
    /// </summary>
    public bool? CleanupExpiredKeys { get; set; }

    /// <summary>
    /// Gets or sets whether to reset daily budgets.
    /// </summary>
    public bool? ResetDailyBudgets { get; set; }

    /// <summary>
    /// Gets or sets whether to reset weekly budgets.
    /// </summary>
    public bool? ResetWeeklyBudgets { get; set; }

    /// <summary>
    /// Gets or sets whether to reset monthly budgets.
    /// </summary>
    public bool? ResetMonthlyBudgets { get; set; }
}

/// <summary>
/// Represents the response from virtual key maintenance operations.
/// </summary>
public class VirtualKeyMaintenanceResponse
{
    /// <summary>
    /// Gets or sets the number of expired keys deleted.
    /// </summary>
    public int? ExpiredKeysDeleted { get; set; }

    /// <summary>
    /// Gets or sets the number of daily budgets reset.
    /// </summary>
    public int? DailyBudgetsReset { get; set; }

    /// <summary>
    /// Gets or sets the number of weekly budgets reset.
    /// </summary>
    public int? WeeklyBudgetsReset { get; set; }

    /// <summary>
    /// Gets or sets the number of monthly budgets reset.
    /// </summary>
    public int? MonthlyBudgetsReset { get; set; }

    /// <summary>
    /// Gets or sets any errors that occurred during maintenance.
    /// </summary>
    public IEnumerable<string>? Errors { get; set; }
}

/// <summary>
/// Represents filter options for virtual key queries.
/// </summary>
public class VirtualKeyFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets whether to filter by enabled status.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether to filter by expired status.
    /// </summary>
    public bool? HasExpired { get; set; }

    /// <summary>
    /// Gets or sets the budget duration to filter by.
    /// </summary>
    public BudgetDuration? BudgetDuration { get; set; }

    /// <summary>
    /// Gets or sets the minimum budget filter.
    /// </summary>
    public decimal? MinBudget { get; set; }

    /// <summary>
    /// Gets or sets the maximum budget filter.
    /// </summary>
    public decimal? MaxBudget { get; set; }

    /// <summary>
    /// Gets or sets the allowed model filter.
    /// </summary>
    public string? AllowedModel { get; set; }

    /// <summary>
    /// Gets or sets the created after date filter.
    /// </summary>
    public DateTime? CreatedAfter { get; set; }

    /// <summary>
    /// Gets or sets the created before date filter.
    /// </summary>
    public DateTime? CreatedBefore { get; set; }

    /// <summary>
    /// Gets or sets the last used after date filter.
    /// </summary>
    public DateTime? LastUsedAfter { get; set; }

    /// <summary>
    /// Gets or sets the last used before date filter.
    /// </summary>
    public DateTime? LastUsedBefore { get; set; }
}

/// <summary>
/// Represents statistics for virtual keys.
/// </summary>
public class VirtualKeyStatistics
{
    /// <summary>
    /// Gets or sets the total number of keys.
    /// </summary>
    public int TotalKeys { get; set; }

    /// <summary>
    /// Gets or sets the number of active keys.
    /// </summary>
    public int ActiveKeys { get; set; }

    /// <summary>
    /// Gets or sets the number of expired keys.
    /// </summary>
    public int ExpiredKeys { get; set; }

    /// <summary>
    /// Gets or sets the total spend across all keys.
    /// </summary>
    public decimal TotalSpend { get; set; }

    /// <summary>
    /// Gets or sets the average spend per key.
    /// </summary>
    public decimal AverageSpendPerKey { get; set; }

    /// <summary>
    /// Gets or sets the number of keys near budget limit.
    /// </summary>
    public int KeysNearBudgetLimit { get; set; }

    /// <summary>
    /// Gets or sets the distribution of keys by budget duration.
    /// </summary>
    public Dictionary<BudgetDuration, int> KeysByDuration { get; set; } = new();
}