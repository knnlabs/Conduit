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

        // Removed hardcoded patterns - now using IModelCapabilityService for all capability detection

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
                _logger.LogError("ModelCapabilityService not available - model capability detection will not function properly");
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
                    _logger.LogError(ex, "Error checking vision capability for model {Model}", modelName);
                    return false;
                }
            }

            _logger.LogWarning("Cannot check vision capability for model {Model} - ModelCapabilityService not available", modelName);
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
            if (request?.Messages == null || request.Messages.Count() == 0)
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
            _logger.LogWarning("GetVisionCapableModels called - this method needs to be made async to properly query ModelCapabilityService");
            // This method should be made async to properly query the capability service
            // For now, return empty list when capability service is not available
            return Enumerable.Empty<string>();
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
