namespace ConduitLLM.AdminClient.Models;

/// <summary>
/// Represents a global system setting.
/// </summary>
public class GlobalSettingDto
{
    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the data type of the setting.
    /// </summary>
    public SettingDataType DataType { get; set; } = SettingDataType.String;

    /// <summary>
    /// Gets or sets the category of the setting.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets whether the setting contains sensitive information.
    /// </summary>
    public bool? IsSecret { get; set; }

    /// <summary>
    /// Gets or sets when the setting was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the setting was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents the data types available for settings.
/// </summary>
public enum SettingDataType
{
    /// <summary>
    /// String data type.
    /// </summary>
    String,

    /// <summary>
    /// Number data type.
    /// </summary>
    Number,

    /// <summary>
    /// Boolean data type.
    /// </summary>
    Boolean,

    /// <summary>
    /// JSON data type.
    /// </summary>
    Json
}

/// <summary>
/// Represents a request to create a global setting.
/// </summary>
public class CreateGlobalSettingDto
{
    /// <summary>
    /// Gets or sets the setting key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the data type of the setting.
    /// </summary>
    public SettingDataType? DataType { get; set; }

    /// <summary>
    /// Gets or sets the category of the setting.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets whether the setting contains sensitive information.
    /// </summary>
    public bool? IsSecret { get; set; }
}

/// <summary>
/// Represents a request to update a global setting.
/// </summary>
public class UpdateGlobalSettingDto
{
    /// <summary>
    /// Gets or sets the setting value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category of the setting.
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Represents a category of settings.
/// </summary>
public class SettingCategory
{
    /// <summary>
    /// Gets or sets the category name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the settings in this category.
    /// </summary>
    public IEnumerable<GlobalSettingDto> Settings { get; set; } = new List<GlobalSettingDto>();
}

/// <summary>
/// Represents audio configuration settings.
/// </summary>
public class AudioConfigurationDto
{
    /// <summary>
    /// Gets or sets the audio provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the audio provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the API key for the audio provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint for the audio provider.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the default voice for text-to-speech.
    /// </summary>
    public string? DefaultVoice { get; set; }

    /// <summary>
    /// Gets or sets the default model for audio processing.
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration for audio processing in seconds.
    /// </summary>
    public int? MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the allowed voices for this provider.
    /// </summary>
    public IEnumerable<string>? AllowedVoices { get; set; }

    /// <summary>
    /// Gets or sets custom settings specific to this provider.
    /// </summary>
    public Dictionary<string, object>? CustomSettings { get; set; }

    /// <summary>
    /// Gets or sets when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents a request to create audio configuration.
/// </summary>
public class CreateAudioConfigurationDto
{
    /// <summary>
    /// Gets or sets the audio provider name.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the audio provider is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the API key for the audio provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint for the audio provider.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the default voice for text-to-speech.
    /// </summary>
    public string? DefaultVoice { get; set; }

    /// <summary>
    /// Gets or sets the default model for audio processing.
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration for audio processing in seconds.
    /// </summary>
    public int? MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the allowed voices for this provider.
    /// </summary>
    public IEnumerable<string>? AllowedVoices { get; set; }

    /// <summary>
    /// Gets or sets custom settings specific to this provider.
    /// </summary>
    public Dictionary<string, object>? CustomSettings { get; set; }
}

/// <summary>
/// Represents a request to update audio configuration.
/// </summary>
public class UpdateAudioConfigurationDto
{
    /// <summary>
    /// Gets or sets whether the audio provider is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the API key for the audio provider.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint for the audio provider.
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the default voice for text-to-speech.
    /// </summary>
    public string? DefaultVoice { get; set; }

    /// <summary>
    /// Gets or sets the default model for audio processing.
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration for audio processing in seconds.
    /// </summary>
    public int? MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the allowed voices for this provider.
    /// </summary>
    public IEnumerable<string>? AllowedVoices { get; set; }

    /// <summary>
    /// Gets or sets custom settings specific to this provider.
    /// </summary>
    public Dictionary<string, object>? CustomSettings { get; set; }
}

/// <summary>
/// Represents router configuration settings.
/// </summary>
public class RouterConfigurationDto
{
    /// <summary>
    /// Gets or sets the routing strategy.
    /// </summary>
    public RoutingStrategy RoutingStrategy { get; set; } = RoutingStrategy.Priority;

    /// <summary>
    /// Gets or sets whether fallback routing is enabled.
    /// </summary>
    public bool FallbackEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the retry delay in milliseconds.
    /// </summary>
    public int RetryDelay { get; set; }

    /// <summary>
    /// Gets or sets whether load balancing is enabled.
    /// </summary>
    public bool LoadBalancingEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether health checking is enabled.
    /// </summary>
    public bool HealthCheckEnabled { get; set; }

    /// <summary>
    /// Gets or sets the health check interval in seconds.
    /// </summary>
    public int HealthCheckInterval { get; set; }

    /// <summary>
    /// Gets or sets whether circuit breaker is enabled.
    /// </summary>
    public bool CircuitBreakerEnabled { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker failure threshold.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker duration in seconds.
    /// </summary>
    public int CircuitBreakerDuration { get; set; }

    /// <summary>
    /// Gets or sets custom routing rules.
    /// </summary>
    public IEnumerable<RouterRule>? CustomRules { get; set; }

    /// <summary>
    /// Gets or sets when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents routing strategies.
/// </summary>
public enum RoutingStrategy
{
    /// <summary>
    /// Route based on priority.
    /// </summary>
    Priority,

    /// <summary>
    /// Round-robin routing.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Route to least cost provider.
    /// </summary>
    LeastCost,

    /// <summary>
    /// Route to fastest provider.
    /// </summary>
    Fastest,

    /// <summary>
    /// Random routing.
    /// </summary>
    Random
}

/// <summary>
/// Represents a custom routing rule.
/// </summary>
public class RouterRule
{
    /// <summary>
    /// Gets or sets the rule ID.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Gets or sets the rule name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the condition for this rule.
    /// </summary>
    public RouterCondition Condition { get; set; } = new();

    /// <summary>
    /// Gets or sets the action to take when the condition is met.
    /// </summary>
    public RouterAction Action { get; set; } = new();

    /// <summary>
    /// Gets or sets the priority of this rule.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets whether this rule is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Represents a condition for routing rules.
/// </summary>
public class RouterCondition
{
    /// <summary>
    /// Gets or sets the type of condition.
    /// </summary>
    public RouterConditionType Type { get; set; } = RouterConditionType.Model;

    /// <summary>
    /// Gets or sets the operator for the condition.
    /// </summary>
    public RouterConditionOperator Operator { get; set; } = RouterConditionOperator.Equals;

    /// <summary>
    /// Gets or sets the value to compare against.
    /// </summary>
    public object Value { get; set; } = new();
}

/// <summary>
/// Represents condition types for routing rules.
/// </summary>
public enum RouterConditionType
{
    /// <summary>
    /// Condition based on model name.
    /// </summary>
    Model,

    /// <summary>
    /// Condition based on virtual key.
    /// </summary>
    Key,

    /// <summary>
    /// Condition based on metadata.
    /// </summary>
    Metadata,

    /// <summary>
    /// Condition based on time.
    /// </summary>
    Time,

    /// <summary>
    /// Condition based on cost.
    /// </summary>
    Cost
}

/// <summary>
/// Represents operators for routing conditions.
/// </summary>
public enum RouterConditionOperator
{
    /// <summary>
    /// Equals operator.
    /// </summary>
    Equals,

    /// <summary>
    /// Contains operator.
    /// </summary>
    Contains,

    /// <summary>
    /// Greater than operator.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Less than operator.
    /// </summary>
    LessThan,

    /// <summary>
    /// Between operator.
    /// </summary>
    Between
}

/// <summary>
/// Represents an action for routing rules.
/// </summary>
public class RouterAction
{
    /// <summary>
    /// Gets or sets the type of action.
    /// </summary>
    public RouterActionType Type { get; set; } = RouterActionType.RouteToProvider;

    /// <summary>
    /// Gets or sets the value for the action.
    /// </summary>
    public object Value { get; set; } = new();
}

/// <summary>
/// Represents action types for routing rules.
/// </summary>
public enum RouterActionType
{
    /// <summary>
    /// Route to a specific provider.
    /// </summary>
    RouteToProvider,

    /// <summary>
    /// Block the request.
    /// </summary>
    Block,

    /// <summary>
    /// Apply rate limiting.
    /// </summary>
    RateLimit,

    /// <summary>
    /// Add metadata to the request.
    /// </summary>
    AddMetadata
}

/// <summary>
/// Represents a request to update router configuration.
/// </summary>
public class UpdateRouterConfigurationDto
{
    /// <summary>
    /// Gets or sets the routing strategy.
    /// </summary>
    public RoutingStrategy? RoutingStrategy { get; set; }

    /// <summary>
    /// Gets or sets whether fallback routing is enabled.
    /// </summary>
    public bool? FallbackEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the retry delay in milliseconds.
    /// </summary>
    public int? RetryDelay { get; set; }

    /// <summary>
    /// Gets or sets whether load balancing is enabled.
    /// </summary>
    public bool? LoadBalancingEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether health checking is enabled.
    /// </summary>
    public bool? HealthCheckEnabled { get; set; }

    /// <summary>
    /// Gets or sets the health check interval in seconds.
    /// </summary>
    public int? HealthCheckInterval { get; set; }

    /// <summary>
    /// Gets or sets whether circuit breaker is enabled.
    /// </summary>
    public bool? CircuitBreakerEnabled { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker failure threshold.
    /// </summary>
    public int? CircuitBreakerThreshold { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker duration in seconds.
    /// </summary>
    public int? CircuitBreakerDuration { get; set; }

    /// <summary>
    /// Gets or sets custom routing rules.
    /// </summary>
    public IEnumerable<RouterRule>? CustomRules { get; set; }
}

/// <summary>
/// Represents the complete system configuration.
/// </summary>
public class SystemConfiguration
{
    /// <summary>
    /// Gets or sets the general settings.
    /// </summary>
    public IEnumerable<GlobalSettingDto> General { get; set; } = new List<GlobalSettingDto>();

    /// <summary>
    /// Gets or sets the audio configurations.
    /// </summary>
    public IEnumerable<AudioConfigurationDto> Audio { get; set; } = new List<AudioConfigurationDto>();

    /// <summary>
    /// Gets or sets the router configuration.
    /// </summary>
    public RouterConfigurationDto Router { get; set; } = new();

    /// <summary>
    /// Gets or sets the setting categories.
    /// </summary>
    public IEnumerable<SettingCategory> Categories { get; set; } = new List<SettingCategory>();
}

/// <summary>
/// Represents filter options for settings queries.
/// </summary>
public class SettingFilters : FilterOptions
{
    /// <summary>
    /// Gets or sets the category filter.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets the data type filter.
    /// </summary>
    public SettingDataType? DataType { get; set; }

    /// <summary>
    /// Gets or sets the secret filter.
    /// </summary>
    public bool? IsSecret { get; set; }

    /// <summary>
    /// Gets or sets the search key filter.
    /// </summary>
    public string? SearchKey { get; set; }
}

/// <summary>
/// Represents options for setting creation.
/// </summary>
public class SettingOptions
{
    /// <summary>
    /// Gets or sets the setting description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the data type.
    /// </summary>
    public SettingDataType? DataType { get; set; }

    /// <summary>
    /// Gets or sets the setting category.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets whether this setting contains sensitive information.
    /// </summary>
    public bool? IsSecret { get; set; }
}

/// <summary>
/// Represents the result of a settings import operation.
/// </summary>
public class SettingsImportResult
{
    /// <summary>
    /// Gets or sets the number of settings imported.
    /// </summary>
    public int Imported { get; set; }

    /// <summary>
    /// Gets or sets the number of settings skipped.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Gets or sets any errors that occurred during import.
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = new List<string>();
}

/// <summary>
/// Represents the result of a configuration validation operation.
/// </summary>
public class ConfigurationValidationResult
{
    /// <summary>
    /// Gets or sets whether the configuration is valid.
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
}