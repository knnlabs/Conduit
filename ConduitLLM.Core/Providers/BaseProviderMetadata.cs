using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers
{
    /// <summary>
    /// Base implementation of IProviderMetadata with common functionality.
    /// Provider-specific implementations should inherit from this class.
    /// </summary>
    public abstract class BaseProviderMetadata : IProviderMetadata
    {
        /// <inheritdoc />
        public abstract ProviderType ProviderType { get; }

        /// <inheritdoc />
        public abstract string DisplayName { get; }

        /// <inheritdoc />
        public abstract string DefaultBaseUrl { get; }

        /// <inheritdoc />
        public virtual ProviderCapabilities Capabilities { get; protected set; }

        /// <inheritdoc />
        public virtual AuthenticationRequirements AuthRequirements { get; protected set; }

        /// <inheritdoc />
        public virtual ProviderConfigurationHints ConfigurationHints { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the BaseProviderMetadata class.
        /// </summary>
        protected BaseProviderMetadata()
        {
            // Initialize with default capabilities
            Capabilities = CreateDefaultCapabilities();
            AuthRequirements = CreateDefaultAuthRequirements();
            ConfigurationHints = CreateDefaultConfigurationHints();
        }

        /// <inheritdoc />
        public virtual ValidationResult ValidateConfiguration(Dictionary<string, object> configuration)
        {
            var errors = new List<ValidationError>();

            // Validate required API key if applicable
            if (AuthRequirements.RequiresApiKey)
            {
                if (!configuration.ContainsKey("apiKey") || 
                    string.IsNullOrWhiteSpace(configuration["apiKey"]?.ToString()))
                {
                    errors.Add(new ValidationError("apiKey", "API key is required"));
                }
            }

            // Validate custom fields
            foreach (var field in AuthRequirements.CustomFields)
            {
                if (field.Required)
                {
                    if (!configuration.ContainsKey(field.Name) || 
                        string.IsNullOrWhiteSpace(configuration[field.Name]?.ToString()))
                    {
                        errors.Add(new ValidationError(field.Name, $"{field.DisplayName} is required"));
                    }
                    else if (!string.IsNullOrEmpty(field.ValidationPattern))
                    {
                        var value = configuration[field.Name]?.ToString() ?? "";
                        if (!Regex.IsMatch(value, field.ValidationPattern))
                        {
                            errors.Add(new ValidationError(field.Name, 
                                $"{field.DisplayName} format is invalid"));
                        }
                    }
                }
            }

            // Validate base URL if provided
            if (configuration.ContainsKey("baseUrl") && 
                !string.IsNullOrWhiteSpace(configuration["baseUrl"]?.ToString()))
            {
                var baseUrl = configuration["baseUrl"].ToString()!;
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errors.Add(new ValidationError("baseUrl", "Base URL must be a valid HTTP(S) URL"));
                }
            }

            return errors.Count() > 0
                ? new ValidationResult { IsValid = false, Errors = errors }
                : ValidationResult.Success();
        }

        /// <summary>
        /// Creates default capabilities. Override in derived classes to customize.
        /// </summary>
        protected virtual ProviderCapabilities CreateDefaultCapabilities()
        {
            return new ProviderCapabilities
            {
                Provider = ProviderType.ToString(),
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = false,
                    TopK = false,
                    Stop = true,
                    PresencePenalty = false,
                    FrequencyPenalty = false,
                    LogitBias = false,
                    N = false,
                    User = false,
                    Seed = false,
                    ResponseFormat = false,
                    Tools = false
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = false,
                    ImageGeneration = false,
                    VisionInput = false,
                    FunctionCalling = false,
                    AudioTranscription = false,
                    TextToSpeech = false
                }
            };
        }

        /// <summary>
        /// Creates default authentication requirements. Override in derived classes to customize.
        /// </summary>
        protected virtual AuthenticationRequirements CreateDefaultAuthRequirements()
        {
            return new AuthenticationRequirements
            {
                RequiresApiKey = true,
                SupportsOAuth = false,
                CustomFields = new List<AuthField>(),
                ApiKeyHeaderName = "Authorization"
            };
        }

        /// <summary>
        /// Creates default configuration hints. Override in derived classes to customize.
        /// </summary>
        protected virtual ProviderConfigurationHints CreateDefaultConfigurationHints()
        {
            return new ProviderConfigurationHints
            {
                RequiresSpecialSetup = false,
                ExampleValues = new Dictionary<string, string>(),
                Tips = new List<ConfigurationTip>()
            };
        }

        /// <summary>
        /// Helper method to create a standard API key field.
        /// </summary>
        protected static AuthField CreateApiKeyField(string displayName = "API Key", 
            string? helpText = null)
        {
            return new AuthField
            {
                Name = "apiKey",
                DisplayName = displayName,
                Required = true,
                Type = AuthFieldType.Password,
                HelpText = helpText
            };
        }

        /// <summary>
        /// Helper method to create a URL field.
        /// </summary>
        protected static AuthField CreateUrlField(string name, string displayName, 
            bool required = false, string? helpText = null)
        {
            return new AuthField
            {
                Name = name,
                DisplayName = displayName,
                Required = required,
                Type = AuthFieldType.Url,
                ValidationPattern = @"^https?://[\w\-._~:/?#[\]@!$&'()*+,;=]+$",
                HelpText = helpText
            };
        }
    }
}