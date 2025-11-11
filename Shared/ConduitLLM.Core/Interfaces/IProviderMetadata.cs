using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines metadata and capabilities for a specific LLM provider.
    /// This interface provides a single source of truth for provider-specific information.
    /// </summary>
    public interface IProviderMetadata
    {
        /// <summary>
        /// Gets the provider type enum value.
        /// </summary>
        ProviderType ProviderType { get; }

        /// <summary>
        /// Gets the human-readable display name for the provider.
        /// Example: "OpenAI", "Azure OpenAI", "Google Vertex AI"
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the default base URL for the provider's API.
        /// This can be overridden in provider configuration.
        /// Example: "https://api.openai.com/v1"
        /// </summary>
        string DefaultBaseUrl { get; }

        /// <summary>
        /// Gets the detailed capabilities supported by this provider.
        /// Includes supported parameters, features, and constraints.
        /// </summary>
        ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the authentication requirements for this provider.
        /// </summary>
        AuthenticationRequirements AuthRequirements { get; }

        /// <summary>
        /// Gets provider-specific configuration hints.
        /// These help guide UI/UX for provider setup.
        /// </summary>
        ProviderConfigurationHints ConfigurationHints { get; }

        /// <summary>
        /// Validates provider-specific configuration.
        /// </summary>
        /// <param name="configuration">The configuration to validate</param>
        /// <returns>Validation result with any error messages</returns>
        ValidationResult ValidateConfiguration(Dictionary<string, object> configuration);
    }

    /// <summary>
    /// Defines authentication requirements for a provider.
    /// </summary>
    public class AuthenticationRequirements
    {
        /// <summary>
        /// Gets whether an API key is required.
        /// </summary>
        public bool RequiresApiKey { get; set; }

        /// <summary>
        /// Gets whether OAuth is supported/required.
        /// </summary>
        public bool SupportsOAuth { get; set; }

        /// <summary>
        /// Gets custom authentication fields required.
        /// Example: Azure requires "DeploymentId" in addition to API key.
        /// </summary>
        public List<AuthField> CustomFields { get; set; } = new();

        /// <summary>
        /// Gets the API key header name if different from default.
        /// Example: "X-API-Key", "Authorization"
        /// </summary>
        public string? ApiKeyHeaderName { get; set; }
    }

    /// <summary>
    /// Defines a custom authentication field.
    /// </summary>
    public class AuthField
    {
        /// <summary>
        /// Gets or sets the field name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the display name for UI.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Gets or sets whether this field is required.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the field type for UI rendering.
        /// </summary>
        public AuthFieldType Type { get; set; } = AuthFieldType.Text;

        /// <summary>
        /// Gets or sets validation pattern if applicable.
        /// </summary>
        public string? ValidationPattern { get; set; }

        /// <summary>
        /// Gets or sets help text for the field.
        /// </summary>
        public string? HelpText { get; set; }
    }

    /// <summary>
    /// Defines the type of authentication field.
    /// </summary>
    public enum AuthFieldType
    {
        Text,
        Password,
        Select,
        Url
    }

    /// <summary>
    /// Provides hints for configuring a provider in the UI.
    /// </summary>
    public class ProviderConfigurationHints
    {
        /// <summary>
        /// Gets or sets documentation URL for the provider.
        /// </summary>
        public string? DocumentationUrl { get; set; }

        /// <summary>
        /// Gets or sets example configuration values.
        /// </summary>
        public Dictionary<string, string> ExampleValues { get; set; } = new();

        /// <summary>
        /// Gets or sets common configuration errors and solutions.
        /// </summary>
        public List<ConfigurationTip> Tips { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this provider requires special setup steps.
        /// </summary>
        public bool RequiresSpecialSetup { get; set; }

        /// <summary>
        /// Gets or sets setup instructions if special setup is required.
        /// </summary>
        public string? SetupInstructions { get; set; }
    }

    /// <summary>
    /// Represents a configuration tip or common issue.
    /// </summary>
    public class ConfigurationTip
    {
        /// <summary>
        /// Gets or sets the tip title.
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Gets or sets the tip description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the tip severity.
        /// </summary>
        public TipSeverity Severity { get; set; } = TipSeverity.Info;
    }

    /// <summary>
    /// Defines the severity of a configuration tip.
    /// </summary>
    public enum TipSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Represents the result of configuration validation.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets or sets whether the validation passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets validation error messages.
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new();

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static ValidationResult Success() => new() { IsValid = true };

        /// <summary>
        /// Creates a failed validation result with a single error.
        /// </summary>
        public static ValidationResult Failure(string field, string message) => new()
        {
            IsValid = false,
            Errors = new List<ValidationError> { new(field, message) }
        };
    }

    /// <summary>
    /// Represents a validation error.
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Gets or sets the field that failed validation.
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Initializes a new instance of the ValidationError class.
        /// </summary>
        public ValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }
    }
}