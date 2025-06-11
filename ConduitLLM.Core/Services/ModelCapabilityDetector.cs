using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Provides detection and validation of model capabilities, particularly for
    /// specialized features like vision/multimodal support.
    /// Now uses IModelCapabilityService for database-driven capability detection.
    /// </summary>
    public class ModelCapabilityDetector : IModelCapabilityDetector
    {
        private readonly ILogger<ModelCapabilityDetector> _logger;
        private readonly IModelCapabilityService? _capabilityService;
        private readonly ILLMClientFactory _clientFactory;

        // Fallback patterns for when capability service is not available
        private static readonly Dictionary<string, List<string>> VisionCapableModelPatterns = new()
        {
            ["openai"] = new List<string> { "gpt-4-vision", "gpt-4-turbo", "gpt-4v", "gpt-4o" },
            ["anthropic"] = new List<string> { "claude-3", "claude-3-opus", "claude-3-sonnet", "claude-3-haiku" },
            ["gemini"] = new List<string> { "gemini", "gemini-pro", "gemini-pro-vision" },
            ["bedrock"] = new List<string> { "claude-3", "claude-3-haiku", "claude-3-sonnet", "claude-3-opus" },
            ["vertexai"] = new List<string> { "gemini" }
        };

        /// <summary>
        /// Initializes a new instance of the ModelCapabilityDetector.
        /// </summary>
        /// <param name="logger">Logger for diagnostics information</param>
        /// <param name="capabilityService">Service for retrieving model capabilities from configuration</param>
        /// <param name="clientFactory">Factory for creating LLM clients</param>
        public ModelCapabilityDetector(
            ILogger<ModelCapabilityDetector> logger,
            IModelCapabilityService? capabilityService,
            ILLMClientFactory clientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _capabilityService = capabilityService;
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

            if (capabilityService == null)
            {
                _logger.LogWarning("ModelCapabilityService not available, falling back to hardcoded patterns");
            }
        }

        /// <summary>
        /// Determines if a model has vision (image processing) capabilities.
        /// </summary>
        /// <param name="modelName">The name of the model to check</param>
        /// <returns>True if the model supports vision input, false otherwise</returns>
        public bool HasVisionCapability(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return false;

            // Use capability service if available
            if (_capabilityService != null)
            {
                try
                {
                    var hasVision = _capabilityService.SupportsVisionAsync(modelName).GetAwaiter().GetResult();
                    return hasVision;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking vision capability for model {Model}, falling back to patterns", modelName);
                }
            }

            // Fallback to pattern matching
            foreach (var patternGroup in VisionCapableModelPatterns)
            {
                foreach (var pattern in patternGroup.Value)
                {
                    if (modelName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a chat completion request contains image content that 
        /// requires a vision-capable model.
        /// </summary>
        /// <param name="request">The chat completion request to check</param>
        /// <returns>True if the request contains image content, false otherwise</returns>
        public bool ContainsImageContent(ChatCompletionRequest request)
        {
            if (request?.Messages == null || !request.Messages.Any())
                return false;

            foreach (var message in request.Messages)
            {
                if (message.Content == null)
                    continue;

                // Check for content that is not a string (likely multimodal)
                if (message.Content is not string)
                {
                    // Handle JsonElement case from deserialization
                    if (message.Content is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            // Look for image_url parts in the content array
                            foreach (var part in jsonElement.EnumerateArray())
                            {
                                if (part.TryGetProperty("type", out var typeProperty) &&
                                    typeProperty.GetString() == "image_url")
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    // Handle collection case from direct API usage
                    else if (message.Content is IEnumerable<object> contentParts)
                    {
                        foreach (var part in contentParts)
                        {
                            if (part is ImageUrlContentPart)
                                return true;

                            // Try to extract type property dynamically
                            var type = part.GetType().GetProperty("Type")?.GetValue(part)?.ToString();
                            if (type == "image_url")
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a list of all available models that support vision capabilities.
        /// </summary>
        /// <returns>A collection of model names that support vision</returns>
        public IEnumerable<string> GetVisionCapableModels()
        {
            // If capability service is available, this method would need to be async
            // For now, return pattern-based models
            var models = new List<string>();

            foreach (var patternGroup in VisionCapableModelPatterns)
            {
                models.AddRange(patternGroup.Value);
            }

            return models.Distinct();
        }

        /// <summary>
        /// Validates that a request can be processed by the specified model.
        /// </summary>
        /// <param name="request">The chat completion request to validate</param>
        /// <param name="modelName">The name of the model to check</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if the request is valid for the model, false otherwise</returns>
        public bool ValidateRequestForModel(ChatCompletionRequest request, string modelName, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (request == null)
            {
                errorMessage = "Request cannot be null";
                return false;
            }

            if (string.IsNullOrEmpty(modelName))
            {
                errorMessage = "Model name cannot be null or empty";
                return false;
            }

            // Check if request contains images but model doesn't support vision
            if (ContainsImageContent(request) && !HasVisionCapability(modelName))
            {
                errorMessage = $"Model '{modelName}' does not support vision/image inputs";
                return false;
            }

            return true;
        }
    }
}
