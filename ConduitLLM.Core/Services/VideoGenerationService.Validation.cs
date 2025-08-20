using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Partial class containing validation and cost estimation functionality for video generation.
    /// </summary>
    public partial class VideoGenerationService
    {
        /// <inheritdoc/>
        public async Task<bool> ValidateRequestAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                _logger.LogWarning("Video generation request validation failed: empty prompt");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                _logger.LogWarning("Video generation request validation failed: empty model");
                return false;
            }

            // Check if model supports video generation
            var supportsVideo = await _capabilityService.SupportsVideoGenerationAsync(request.Model);
            if (!supportsVideo)
            {
                _logger.LogWarning("Model {Model} does not support video generation", request.Model);
                return false;
            }

            // Validate duration if specified
            if (request.Duration.HasValue && (request.Duration.Value < 1 || request.Duration.Value > 60))
            {
                _logger.LogWarning("Invalid video duration: {Duration}", request.Duration);
                return false;
            }

            // Validate FPS if specified
            if (request.Fps.HasValue && (request.Fps.Value < 1 || request.Fps.Value > 120))
            {
                _logger.LogWarning("Invalid video FPS: {Fps}", request.Fps);
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<decimal> EstimateCostAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            // Create usage object for cost calculation
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = request.Duration ?? 6, // Default 6 seconds
                VideoResolution = request.Size ?? "1280x720"
            };

            // Use the cost calculation service
            return await _costService.CalculateCostAsync(request.Model, usage, cancellationToken);
        }
    }
}