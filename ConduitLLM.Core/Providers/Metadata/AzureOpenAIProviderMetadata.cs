using System.Collections.Generic;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Providers.Metadata
{
    /// <summary>
    /// Provider metadata for Azure OpenAI Service.
    /// </summary>
    public class AzureOpenAIProviderMetadata : BaseProviderMetadata
    {
        /// <inheritdoc />
        public override ProviderType ProviderType => ProviderType.AzureOpenAI;

        /// <inheritdoc />
        public override string DisplayName => "Azure OpenAI";

        /// <inheritdoc />
        public override string DefaultBaseUrl => "https://{resource-name}.openai.azure.com";

        /// <summary>
        /// Initializes a new instance of the AzureOpenAIProviderMetadata class.
        /// </summary>
        public AzureOpenAIProviderMetadata()
        {
            // Azure OpenAI has same capabilities as OpenAI
            Capabilities = new ProviderCapabilities
            {
                Provider = ProviderType.ToString(),
                ChatParameters = new ChatParameterSupport
                {
                    Temperature = true,
                    MaxTokens = true,
                    TopP = true,
                    Stop = true,
                    PresencePenalty = true,
                    FrequencyPenalty = true,
                    LogitBias = true,
                    N = true,
                    User = true,
                    Seed = true,
                    ResponseFormat = true,
                    Tools = true,
                    Constraints = new ParameterConstraints
                    {
                        TemperatureRange = new Range<double>(0.0, 2.0),
                        TopPRange = new Range<double>(0.0, 1.0),
                        MaxTokenLimit = 128000
                    }
                },
                Features = new FeatureSupport
                {
                    Streaming = true,
                    Embeddings = true,
                    ImageGeneration = true,
                    VisionInput = true,
                    FunctionCalling = true,
                    AudioTranscription = true,
                    TextToSpeech = true
                }
            };

            // Configure Azure-specific authentication
            AuthRequirements = new AuthenticationRequirements
            {
                RequiresApiKey = true,
                SupportsOAuth = true, // Azure supports both API key and OAuth
                ApiKeyHeaderName = "api-key",
                CustomFields = new List<AuthField>
                {
                    new AuthField
                    {
                        Name = "apiKey",
                        DisplayName = "Azure API Key",
                        Required = true,
                        Type = AuthFieldType.Password,
                        HelpText = "Your Azure OpenAI resource key"
                    },
                    new AuthField
                    {
                        Name = "resourceName",
                        DisplayName = "Resource Name",
                        Required = true,
                        Type = AuthFieldType.Text,
                        ValidationPattern = @"^[a-zA-Z0-9-]+$",
                        HelpText = "Your Azure OpenAI resource name (e.g., 'my-openai-resource')"
                    },
                    new AuthField
                    {
                        Name = "deploymentId",
                        DisplayName = "Deployment ID",
                        Required = true,
                        Type = AuthFieldType.Text,
                        HelpText = "The deployment name you chose when deploying the model"
                    },
                    new AuthField
                    {
                        Name = "apiVersion",
                        DisplayName = "API Version",
                        Required = false,
                        Type = AuthFieldType.Text,
                        ValidationPattern = @"^\d{4}-\d{2}-\d{2}(-preview)?$",
                        HelpText = "API version (e.g., '2024-02-01'). If not specified, latest stable version is used."
                    }
                }
            };

            // Configure hints
            ConfigurationHints = new ProviderConfigurationHints
            {
                DocumentationUrl = "https://learn.microsoft.com/en-us/azure/ai-services/openai/",
                RequiresSpecialSetup = true,
                SetupInstructions = "1. Create an Azure OpenAI resource in Azure Portal\n" +
                                   "2. Deploy a model (e.g., gpt-4) with a deployment name\n" +
                                   "3. Copy your resource name and API key\n" +
                                   "4. Use the deployment name as the model ID in requests",
                ExampleValues = new Dictionary<string, string>
                {
                    ["resourceName"] = "my-openai-resource",
                    ["deploymentId"] = "gpt-4-deployment",
                    ["apiVersion"] = "2024-02-01",
                    ["baseUrl"] = "https://my-openai-resource.openai.azure.com"
                },
                Tips = new List<ConfigurationTip>
                {
                    new ConfigurationTip
                    {
                        Title = "Deployment vs Model Names",
                        Description = "In Azure, you use deployment names instead of model names. Map your deployment names to model aliases.",
                        Severity = TipSeverity.Warning
                    },
                    new ConfigurationTip
                    {
                        Title = "Regional Availability",
                        Description = "Not all models are available in all regions. Check Azure documentation for availability.",
                        Severity = TipSeverity.Info
                    },
                    new ConfigurationTip
                    {
                        Title = "API Version",
                        Description = "Azure OpenAI API versions may lag behind OpenAI's latest features.",
                        Severity = TipSeverity.Info
                    }
                }
            };
        }

        /// <inheritdoc />
        public override ValidationResult ValidateConfiguration(Dictionary<string, object> configuration)
        {
            var errors = new List<ValidationError>();

            // Validate required fields
            if (!configuration.ContainsKey("apiKey") || 
                string.IsNullOrWhiteSpace(configuration["apiKey"]?.ToString()))
            {
                errors.Add(new ValidationError("apiKey", "Azure API key is required"));
            }

            if (!configuration.ContainsKey("resourceName") || 
                string.IsNullOrWhiteSpace(configuration["resourceName"]?.ToString()))
            {
                errors.Add(new ValidationError("resourceName", "Azure resource name is required"));
            }

            if (!configuration.ContainsKey("deploymentId") || 
                string.IsNullOrWhiteSpace(configuration["deploymentId"]?.ToString()))
            {
                errors.Add(new ValidationError("deploymentId", "Azure deployment ID is required"));
            }

            // Validate base URL construction
            if (configuration.TryGetValue("baseUrl", out var baseUrl) && 
                !string.IsNullOrWhiteSpace(baseUrl?.ToString()))
            {
                var url = baseUrl.ToString()!;
                if (!url.Contains(".openai.azure.com"))
                {
                    errors.Add(new ValidationError("baseUrl", 
                        "Azure OpenAI base URL must contain '.openai.azure.com'"));
                }
            }

            return errors.Count > 0
                ? new ValidationResult { IsValid = false, Errors = errors }
                : ValidationResult.Success();
        }
    }
}