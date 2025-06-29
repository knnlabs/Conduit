using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents the filter type for IP filtering.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterType
{
    /// <summary>
    /// Allow access for IPs matching this filter (whitelist).
    /// </summary>
    [JsonStringEnumMemberName("whitelist")]
    Allow,

    /// <summary>
    /// Deny access for IPs matching this filter (blacklist).
    /// </summary>
    [JsonStringEnumMemberName("blacklist")]
    Deny
}

/// <summary>
/// Represents the filter mode for IP filtering.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterMode
{
    /// <summary>
    /// Permissive mode - allow by default.
    /// </summary>
    [JsonStringEnumMemberName("permissive")]
    Permissive,

    /// <summary>
    /// Restrictive mode - deny by default.
    /// </summary>
    [JsonStringEnumMemberName("restrictive")]
    Restrictive
}

/// <summary>
/// Represents an IP filter rule.
/// </summary>
public class IpFilterDto
{
    /// <summary>
    /// Gets or sets the filter ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the filter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CIDR range for this filter.
    /// </summary>
    [JsonPropertyName("ipAddressOrCidr")]
    public string CidrRange { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter type (Allow or Deny).
    /// </summary>
    public FilterType FilterType { get; set; }

    /// <summary>
    /// Gets or sets whether this filter is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the optional description for this filter.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this filter was last matched.
    /// </summary>
    public DateTime? LastMatchedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times this filter has been matched.
    /// </summary>
    public int? MatchCount { get; set; }
}

/// <summary>
/// Represents a request to create a new IP filter.
/// </summary>
public class CreateIpFilterDto
{
    /// <summary>
    /// Gets or sets the filter name.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CIDR range for this filter.
    /// </summary>
    [Required]
    [JsonPropertyName("ipAddressOrCidr")]
    public string CidrRange { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter type (Allow or Deny).
    /// </summary>
    [Required]
    public FilterType FilterType { get; set; }

    /// <summary>
    /// Gets or sets whether this filter is enabled (default: true).
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the optional description for this filter.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// Represents a request to update an existing IP filter.
/// </summary>
public class UpdateIpFilterDto
{
    /// <summary>
    /// Gets or sets the filter ID.
    /// </summary>
    [Required]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the filter name.
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the CIDR range for this filter.
    /// </summary>
    [JsonPropertyName("ipAddressOrCidr")]
    public string? CidrRange { get; set; }

    /// <summary>
    /// Gets or sets the filter type (Allow or Deny).
    /// </summary>
    public FilterType? FilterType { get; set; }

    /// <summary>
    /// Gets or sets whether this filter is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the optional description for this filter.
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// Represents IP filter system settings.
/// </summary>
public class IpFilterSettingsDto
{
    /// <summary>
    /// Gets or sets whether IP filtering is enabled globally.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the default action when no filters match.
    /// </summary>
    public bool DefaultAllow { get; set; }

    /// <summary>
    /// Gets or sets whether to bypass filtering for admin UI.
    /// </summary>
    public bool BypassForAdminUi { get; set; }

    /// <summary>
    /// Gets or sets endpoints that are excluded from filtering.
    /// </summary>
    public IEnumerable<string> ExcludedEndpoints { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the filter mode.
    /// </summary>
    public FilterMode FilterMode { get; set; }

    /// <summary>
    /// Gets or sets the whitelist (allow) filters.
    /// </summary>
    public IEnumerable<IpFilterDto> WhitelistFilters { get; set; } = new List<IpFilterDto>();

    /// <summary>
    /// Gets or sets the blacklist (deny) filters.
    /// </summary>
    public IEnumerable<IpFilterDto> BlacklistFilters { get; set; } = new List<IpFilterDto>();

    /// <summary>
    /// Gets or sets the maximum number of filters per type.
    /// </summary>
    public int? MaxFiltersPerType { get; set; }

    /// <summary>
    /// Gets or sets whether IPv6 filtering is enabled.
    /// </summary>
    public bool? Ipv6Enabled { get; set; }
}

/// <summary>
/// Represents a request to update IP filter settings.
/// </summary>
public class UpdateIpFilterSettingsDto
{
    /// <summary>
    /// Gets or sets whether IP filtering is enabled globally.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the default action when no filters match.
    /// </summary>
    public bool? DefaultAllow { get; set; }

    /// <summary>
    /// Gets or sets whether to bypass filtering for admin UI.
    /// </summary>
    public bool? BypassForAdminUi { get; set; }

    /// <summary>
    /// Gets or sets endpoints that are excluded from filtering.
    /// </summary>
    public IEnumerable<string>? ExcludedEndpoints { get; set; }

    /// <summary>
    /// Gets or sets the filter mode.
    /// </summary>
    public FilterMode? FilterMode { get; set; }

    /// <summary>
    /// Gets or sets whether IPv6 filtering is enabled.
    /// </summary>
    public bool? Ipv6Enabled { get; set; }
}

/// <summary>
/// Represents a request to check an IP address against filters.
/// </summary>
public class IpCheckRequest
{
    /// <summary>
    /// Gets or sets the IP address to check.
    /// </summary>
    [Required]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional endpoint being accessed.
    /// </summary>
    public string? Endpoint { get; set; }
}

/// <summary>
/// Represents the result of an IP check.
/// </summary>
public class IpCheckResult
{
    /// <summary>
    /// Gets or sets whether the IP is allowed.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Gets or sets the reason for denial if the IP is not allowed.
    /// </summary>
    [JsonPropertyName("deniedReason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the name of the matched filter.
    /// </summary>
    public string? MatchedFilter { get; set; }

    /// <summary>
    /// Gets or sets the ID of the matched filter.
    /// </summary>
    public int? MatchedFilterId { get; set; }

    /// <summary>
    /// Gets or sets the type of the matched filter.
    /// </summary>
    public FilterType? FilterType { get; set; }

    /// <summary>
    /// Gets or sets whether this was a default action.
    /// </summary>
    public bool? IsDefaultAction { get; set; }
}

/// <summary>
/// Represents filter criteria for IP filter queries.
/// </summary>
public class IpFilterFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the filter type filter.
    /// </summary>
    public FilterType? FilterType { get; set; }

    /// <summary>
    /// Gets or sets the enabled status filter.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the name contains filter.
    /// </summary>
    public string? NameContains { get; set; }

    /// <summary>
    /// Gets or sets the CIDR contains filter.
    /// </summary>
    public string? CidrContains { get; set; }

    /// <summary>
    /// Gets or sets the last matched after filter.
    /// </summary>
    public DateTime? LastMatchedAfter { get; set; }

    /// <summary>
    /// Gets or sets the last matched before filter.
    /// </summary>
    public DateTime? LastMatchedBefore { get; set; }

    /// <summary>
    /// Gets or sets the minimum match count filter.
    /// </summary>
    public int? MinMatchCount { get; set; }
}

/// <summary>
/// Represents IP filter statistics.
/// </summary>
public class IpFilterStatistics
{
    /// <summary>
    /// Gets or sets the total number of filters.
    /// </summary>
    public int TotalFilters { get; set; }

    /// <summary>
    /// Gets or sets the number of enabled filters.
    /// </summary>
    public int EnabledFilters { get; set; }

    /// <summary>
    /// Gets or sets the number of allow filters.
    /// </summary>
    public int AllowFilters { get; set; }

    /// <summary>
    /// Gets or sets the number of deny filters.
    /// </summary>
    public int DenyFilters { get; set; }

    /// <summary>
    /// Gets or sets the total number of matches.
    /// </summary>
    public int TotalMatches { get; set; }

    /// <summary>
    /// Gets or sets recent matches.
    /// </summary>
    public IEnumerable<RecentMatch> RecentMatches { get; set; } = new List<RecentMatch>();

    /// <summary>
    /// Gets or sets the top matched filters.
    /// </summary>
    public IEnumerable<TopMatchedFilter> TopMatchedFilters { get; set; } = new List<TopMatchedFilter>();
}

/// <summary>
/// Represents a recent filter match.
/// </summary>
public class RecentMatch
{
    /// <summary>
    /// Gets or sets the timestamp of the match.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the IP address that was matched.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the filter that matched.
    /// </summary>
    public string FilterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action taken (allowed or denied).
    /// </summary>
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// Represents a top matched filter.
/// </summary>
public class TopMatchedFilter
{
    /// <summary>
    /// Gets or sets the filter ID.
    /// </summary>
    public int FilterId { get; set; }

    /// <summary>
    /// Gets or sets the filter name.
    /// </summary>
    public string FilterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the match count.
    /// </summary>
    public int MatchCount { get; set; }
}

/// <summary>
/// Represents a bulk IP filter request.
/// </summary>
public class BulkIpFilterRequest
{
    /// <summary>
    /// Gets or sets the filters to create.
    /// </summary>
    [Required]
    public IEnumerable<CreateIpFilterDto> Filters { get; set; } = new List<CreateIpFilterDto>();

    /// <summary>
    /// Gets or sets whether to replace existing filters.
    /// </summary>
    public bool? ReplaceExisting { get; set; }

    /// <summary>
    /// Gets or sets the filter type for all filters in the request.
    /// </summary>
    public FilterType? FilterType { get; set; }
}

/// <summary>
/// Represents the response from a bulk IP filter operation.
/// </summary>
public class BulkIpFilterResponse
{
    /// <summary>
    /// Gets or sets the successfully created filters.
    /// </summary>
    public IEnumerable<IpFilterDto> Created { get; set; } = new List<IpFilterDto>();

    /// <summary>
    /// Gets or sets the successfully updated filters.
    /// </summary>
    public IEnumerable<IpFilterDto> Updated { get; set; } = new List<IpFilterDto>();

    /// <summary>
    /// Gets or sets the failed filter operations.
    /// </summary>
    public IEnumerable<BulkIpFilterFailure> Failed { get; set; } = new List<BulkIpFilterFailure>();
}

/// <summary>
/// Represents a failed bulk IP filter operation.
/// </summary>
public class BulkIpFilterFailure
{
    /// <summary>
    /// Gets or sets the index of the failed filter in the original request.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filter that failed.
    /// </summary>
    public CreateIpFilterDto? Filter { get; set; }
}

/// <summary>
/// Represents the result of IP filter validation.
/// </summary>
public class IpFilterValidationResult
{
    /// <summary>
    /// Gets or sets whether the filter is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets any validation errors.
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets any validation warnings.
    /// </summary>
    public IEnumerable<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a suggested CIDR range if the original is invalid.
    /// </summary>
    public string? SuggestedCidr { get; set; }

    /// <summary>
    /// Gets or sets any overlapping filters.
    /// </summary>
    public IEnumerable<OverlappingFilter> OverlappingFilters { get; set; } = new List<OverlappingFilter>();
}

/// <summary>
/// Represents an overlapping filter.
/// </summary>
public class OverlappingFilter
{
    /// <summary>
    /// Gets or sets the filter ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the filter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the CIDR range.
    /// </summary>
    public string CidrRange { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of testing IP filter rules.
/// </summary>
public class IpFilterTestResult
{
    /// <summary>
    /// Gets or sets the current result with existing rules.
    /// </summary>
    public IpCheckResult CurrentResult { get; set; } = new();

    /// <summary>
    /// Gets or sets the proposed result with new rules.
    /// </summary>
    public IpCheckResult? ProposedResult { get; set; }

    /// <summary>
    /// Gets or sets the changes that would be made.
    /// </summary>
    public IEnumerable<string> Changes { get; set; } = new List<string>();
}