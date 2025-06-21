namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a provider credential in the system.
/// </summary>
public class ProviderCredentialDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the provider credential.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the provider.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for the provider (may be masked for security).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint URL for the provider.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the organization ID for the provider.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets additional configuration as JSON string.
    /// </summary>
    public string? AdditionalConfig { get; set; }

    /// <summary>
    /// Gets or sets whether the provider credential is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets when the credential was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the credential was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents a request to create a new provider credential.
/// </summary>
public class CreateProviderCredentialDto
{
    /// <summary>
    /// Gets or sets the name of the provider.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint URL for the provider.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the organization ID for the provider.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets additional configuration as JSON string.
    /// </summary>
    public string? AdditionalConfig { get; set; }

    /// <summary>
    /// Gets or sets whether the provider credential should be enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Represents a request to update an existing provider credential.
/// </summary>
public class UpdateProviderCredentialDto
{
    /// <summary>
    /// Gets or sets the API key for the provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint URL for the provider.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the organization ID for the provider.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets additional configuration as JSON string.
    /// </summary>
    public string? AdditionalConfig { get; set; }

    /// <summary>
    /// Gets or sets whether the provider credential is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Represents a request to test a provider connection.
/// </summary>
public class ProviderConnectionTestRequest
{
    /// <summary>
    /// Gets or sets the name of the provider to test.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key for testing.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint URL for testing.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the organization ID for testing.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets additional configuration for testing.
    /// </summary>
    public string? AdditionalConfig { get; set; }
}

/// <summary>
/// Represents the result of a provider connection test.
/// </summary>
public class ProviderConnectionTestResultDto
{
    /// <summary>
    /// Gets or sets whether the connection test was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the result message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets error details if the test failed.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Gets or sets the provider name that was tested.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of available models from the provider.
    /// </summary>
    public IEnumerable<string>? ModelsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public int? ResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the test.
    /// </summary>
    public DateTime? Timestamp { get; set; }
}

/// <summary>
/// Represents metadata about a provider.
/// </summary>
public class ProviderDataDto
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for the provider.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of supported models.
    /// </summary>
    public IEnumerable<string> SupportedModels { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets whether the provider requires an API key.
    /// </summary>
    public bool RequiresApiKey { get; set; }

    /// <summary>
    /// Gets or sets whether the provider requires a custom endpoint.
    /// </summary>
    public bool RequiresEndpoint { get; set; }

    /// <summary>
    /// Gets or sets whether the provider requires an organization ID.
    /// </summary>
    public bool RequiresOrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the configuration schema for the provider.
    /// </summary>
    public Dictionary<string, object>? ConfigSchema { get; set; }
}

/// <summary>
/// Represents health configuration for a provider.
/// </summary>
public class ProviderHealthConfigurationDto
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether health checking is enabled for this provider.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the interval between health checks in seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timeout for health checks in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failures before marking as unhealthy.
    /// </summary>
    public int UnhealthyThreshold { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive successes before marking as healthy.
    /// </summary>
    public int HealthyThreshold { get; set; }

    /// <summary>
    /// Gets or sets the test model to use for health checks.
    /// </summary>
    public string? TestModel { get; set; }

    /// <summary>
    /// Gets or sets the last check time.
    /// </summary>
    public DateTime? LastCheckTime { get; set; }

    /// <summary>
    /// Gets or sets whether the provider is currently healthy.
    /// </summary>
    public bool? IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failures.
    /// </summary>
    public int? ConsecutiveFailures { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive successes.
    /// </summary>
    public int? ConsecutiveSuccesses { get; set; }
}

/// <summary>
/// Represents a request to update provider health configuration.
/// </summary>
public class UpdateProviderHealthConfigurationDto
{
    /// <summary>
    /// Gets or sets whether health checking is enabled for this provider.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the interval between health checks in seconds.
    /// </summary>
    public int? CheckIntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timeout for health checks in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failures before marking as unhealthy.
    /// </summary>
    public int? UnhealthyThreshold { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive successes before marking as healthy.
    /// </summary>
    public int? HealthyThreshold { get; set; }

    /// <summary>
    /// Gets or sets the test model to use for health checks.
    /// </summary>
    public string? TestModel { get; set; }
}

/// <summary>
/// Represents a provider health check record.
/// </summary>
public class ProviderHealthRecordDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the health record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time of the health check.
    /// </summary>
    public DateTime CheckTime { get; set; }

    /// <summary>
    /// Gets or sets whether the provider was healthy at check time.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the response time in milliseconds.
    /// </summary>
    public int? ResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the error message if the check failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code from the health check.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the models that were checked.
    /// </summary>
    public IEnumerable<string>? ModelsChecked { get; set; }
}

/// <summary>
/// Represents the current health status of a provider.
/// </summary>
public class ProviderHealthStatusDto
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the provider is currently healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the last check time.
    /// </summary>
    public DateTime? LastCheckTime { get; set; }

    /// <summary>
    /// Gets or sets the last successful check time.
    /// </summary>
    public DateTime? LastSuccessTime { get; set; }

    /// <summary>
    /// Gets or sets the last failed check time.
    /// </summary>
    public DateTime? LastFailureTime { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive successes.
    /// </summary>
    public int ConsecutiveSuccesses { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double? AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the uptime percentage.
    /// </summary>
    public double? Uptime { get; set; }

    /// <summary>
    /// Gets or sets the error rate percentage.
    /// </summary>
    public double? ErrorRate { get; set; }
}

/// <summary>
/// Represents a summary of provider health across all providers.
/// </summary>
public class ProviderHealthSummaryDto
{
    /// <summary>
    /// Gets or sets the total number of providers.
    /// </summary>
    public int TotalProviders { get; set; }

    /// <summary>
    /// Gets or sets the number of healthy providers.
    /// </summary>
    public int HealthyProviders { get; set; }

    /// <summary>
    /// Gets or sets the number of unhealthy providers.
    /// </summary>
    public int UnhealthyProviders { get; set; }

    /// <summary>
    /// Gets or sets the number of unconfigured providers.
    /// </summary>
    public int UnconfiguredProviders { get; set; }

    /// <summary>
    /// Gets or sets the detailed status of each provider.
    /// </summary>
    public IEnumerable<ProviderHealthStatusDto> Providers { get; set; } = new List<ProviderHealthStatusDto>();
}

/// <summary>
/// Represents filter options for provider queries.
/// </summary>
public class ProviderFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets whether to filter by enabled status.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the provider name to filter by.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets whether to filter by API key presence.
    /// </summary>
    public bool? HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets whether to filter by health status.
    /// </summary>
    public bool? IsHealthy { get; set; }
}

/// <summary>
/// Represents filter options for provider health queries.
/// </summary>
public class ProviderHealthFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the provider name to filter by.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets whether to filter by health status.
    /// </summary>
    public bool? IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the start date for filtering health records.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for filtering health records.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the minimum response time filter.
    /// </summary>
    public int? MinResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the maximum response time filter.
    /// </summary>
    public int? MaxResponseTime { get; set; }
}

/// <summary>
/// Represents usage statistics for a provider.
/// </summary>
public class ProviderUsageStatistics
{
    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of requests.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of successful requests.
    /// </summary>
    public int SuccessfulRequests { get; set; }

    /// <summary>
    /// Gets or sets the number of failed requests.
    /// </summary>
    public int FailedRequests { get; set; }

    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the total cost for this provider.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Gets or sets the usage count by model.
    /// </summary>
    public Dictionary<string, int> ModelsUsed { get; set; } = new();

    /// <summary>
    /// Gets or sets the error count by type.
    /// </summary>
    public Dictionary<string, int> ErrorTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the time range for these statistics.
    /// </summary>
    public TimeRange TimeRange { get; set; } = new();
}

/// <summary>
/// Represents a time range for statistics.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime End { get; set; }
}