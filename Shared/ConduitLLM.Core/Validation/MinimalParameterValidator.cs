using System.Text.Json;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Validation
{
    /// <summary>
    /// Provides minimal, provider-agnostic validation for model parameters.
    /// This validator only prevents the most obvious errors and removes null values,
    /// allowing providers to handle their own specific requirements.
    /// </summary>
    public class MinimalParameterValidator
    {
        private readonly ILogger<MinimalParameterValidator> _logger;

        public MinimalParameterValidator(ILogger<MinimalParameterValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Performs minimal validation on chat completion request parameters.
        /// Only prevents obvious errors like negative values where they make no sense.
        /// </summary>
        public void ValidateTextParameters(ChatCompletionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("Model is required for routing", nameof(request));
            }

            if (request.Messages == null || request.Messages.Count == 0)
            {
                throw new ArgumentException("At least one message is required", nameof(request));
            }

            // Only validate obviously wrong values
            if (request.Temperature.HasValue && request.Temperature.Value < 0)
            {
                _logger.LogWarning("Temperature {Temperature} is negative. Setting to 0", request.Temperature.Value);
                request.Temperature = 0;
            }

            if (request.MaxTokens.HasValue && request.MaxTokens.Value < 0)
            {
                _logger.LogWarning("MaxTokens {MaxTokens} is negative. Removing parameter", request.MaxTokens.Value);
                request.MaxTokens = null;
            }

            if (request.N.HasValue && request.N.Value < 1)
            {
                _logger.LogWarning("N {N} is less than 1. Setting to 1", request.N.Value);
                request.N = 1;
            }

            // Clean up extension data
            CleanExtensionData(request.ExtensionData);
        }

        /// <summary>
        /// Performs minimal validation on image generation request parameters.
        /// Only prevents obvious errors like missing prompt.
        /// </summary>
        public void ValidateImageParameters(ImageGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("Model is required for routing", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                throw new ArgumentException("Prompt is required for image generation", nameof(request));
            }

            // Only validate obviously wrong values
            if (request.N < 1)
            {
                _logger.LogWarning("Image N {N} is less than 1. Setting to 1", request.N);
                request.N = 1;
            }

            // Clean up extension data
            CleanExtensionData(request.ExtensionData);
        }

        /// <summary>
        /// Performs minimal validation on video generation request parameters.
        /// Only prevents obvious errors like missing prompt.
        /// </summary>
        public void ValidateVideoParameters(VideoGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException("Model is required for routing", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                throw new ArgumentException("Prompt is required for video generation", nameof(request));
            }

            // Only validate obviously wrong values
            if (request.N < 1)
            {
                _logger.LogWarning("Video N {N} is less than 1. Setting to 1", request.N);
                request.N = 1;
            }

            if (request.Duration.HasValue && request.Duration.Value < 0)
            {
                _logger.LogWarning("Video duration {Duration} is negative. Removing parameter", request.Duration.Value);
                request.Duration = null;
            }

            if (request.Fps.HasValue && request.Fps.Value < 0)
            {
                _logger.LogWarning("Video FPS {Fps} is negative. Removing parameter", request.Fps.Value);
                request.Fps = null;
            }

            // Clean up extension data
            CleanExtensionData(request.ExtensionData);
        }

        /// <summary>
        /// Removes null and undefined values from extension data.
        /// This prevents unnecessary data from being sent to providers.
        /// </summary>
        private void CleanExtensionData(Dictionary<string, JsonElement>? extensionData)
        {
            if (extensionData == null || extensionData.Count == 0)
            {
                return;
            }

            var keysToRemove = new List<string>();

            foreach (var kvp in extensionData)
            {
                // Remove null or undefined values
                if (kvp.Value.ValueKind == JsonValueKind.Null || 
                    kvp.Value.ValueKind == JsonValueKind.Undefined)
                {
                    _logger.LogDebug("Removing null/undefined extension parameter '{Key}'", kvp.Key);
                    keysToRemove.Add(kvp.Key);
                    continue;
                }

                // Only check for extremely unreasonable numeric values
                if (kvp.Value.ValueKind == JsonValueKind.Number)
                {
                    var key = kvp.Key.ToLowerInvariant();
                    
                    // Check for negative values in parameters that should never be negative
                    if ((key.Contains("tokens") || key.Contains("steps") || key.Contains("count") || 
                         key.Contains("width") || key.Contains("height") || key.Contains("seed")) &&
                        kvp.Value.TryGetDouble(out var value) && value < 0)
                    {
                        _logger.LogWarning("Extension parameter '{Key}' has negative value {Value}. Removing", 
                            kvp.Key, value);
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            // Remove problematic keys
            foreach (var key in keysToRemove)
            {
                extensionData.Remove(key);
            }
        }
    }
}