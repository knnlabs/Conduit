using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    public partial class ReplicateClient
    {
        /// <inheritdoc/>
        public override async Task<ImageGenerationResponse> CreateImageAsync(
            ImageGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateImageAsync");

            Logger.LogInformation("Creating image with Replicate for model '{ModelId}'", ProviderModelId);

            try
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToImageGenerationRequest(request);
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                // Process the final result
                return MapToImageGenerationResponse(finalPrediction, request.Model);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Replicate image generation");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a video using Replicate's prediction API.
        /// </summary>
        /// <param name="request">The video generation request containing the prompt and generation parameters.</param>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The video generation response containing URLs to the generated video(s).</returns>
        public async Task<VideoGenerationResponse> CreateVideoAsync(
            VideoGenerationRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "CreateVideoAsync");

            Logger.LogInformation("Creating video with Replicate for model '{ModelId}'", ProviderModelId);

            try
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToVideoGenerationRequest(request);
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                // Process the final result
                return MapToVideoGenerationResponse(finalPrediction, request.Model);
            }
            catch (LLMCommunicationException)
            {
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Replicate video generation");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        private ReplicatePredictionRequest MapToImageGenerationRequest(ImageGenerationRequest request)
        {
            // Prepare the input based on the model
            var input = new Dictionary<string, object>
            {
                ["prompt"] = request.Prompt
            };

            // Add optional parameters if provided
            if (request.Size != null)
            {
                var dimensions = request.Size.Split('x');
                if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int width) && int.TryParse(dimensions[1], out int height))
                {
                    input["width"] = width;
                    input["height"] = height;
                }
            }

            if (request.Quality != null)
            {
                input["quality"] = request.Quality;
            }

            if (request.Style != null)
            {
                input["style"] = request.Style;
            }

            if (request.N > 1)
            {
                input["num_outputs"] = request.N;
            }

            return new ReplicatePredictionRequest
            {
                Version = ProviderModelId,
                Input = input
            };
        }

        private ReplicatePredictionRequest MapToVideoGenerationRequest(VideoGenerationRequest request)
        {
            // Prepare the input based on the model
            var input = new Dictionary<string, object>
            {
                ["prompt"] = request.Prompt
            };

            // Add optional parameters if provided
            if (request.Duration.HasValue)
            {
                // Most video models use "duration" or "num_seconds"
                input["duration"] = request.Duration.Value;
                input["num_seconds"] = request.Duration.Value;
            }

            if (request.Size != null)
            {
                // Parse size like "1280x720" into width and height
                var dimensions = request.Size.Split('x');
                if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int width) && int.TryParse(dimensions[1], out int height))
                {
                    input["width"] = width;
                    input["height"] = height;
                }
                else
                {
                    // Some models use resolution directly
                    input["resolution"] = request.Size;
                }
            }

            if (request.Fps.HasValue)
            {
                input["fps"] = request.Fps.Value;
            }

            if (request.Seed.HasValue)
            {
                input["seed"] = request.Seed.Value;
            }

            if (request.N > 1)
            {
                input["num_outputs"] = request.N;
            }

            return new ReplicatePredictionRequest
            {
                Version = ProviderModelId,
                Input = input
            };
        }

        private ImageGenerationResponse MapToImageGenerationResponse(ReplicatePredictionResponse prediction, string originalModelAlias)
        {
            // Extract image URLs from the prediction output
            var imageUrls = ExtractImageUrlsFromPredictionOutput(prediction.Output);

            return new ImageGenerationResponse
            {
                Created = ((DateTimeOffset)prediction.CreatedAt).ToUnixTimeSeconds(),
                Data = imageUrls.Select(url => new Core.Models.ImageData
                {
                    Url = url
                }).ToList()
            };
        }

        private VideoGenerationResponse MapToVideoGenerationResponse(ReplicatePredictionResponse prediction, string originalModelAlias)
        {
            // Extract video URLs from the prediction output
            var videoUrls = ExtractVideoUrlsFromPredictionOutput(prediction.Output);

            return new VideoGenerationResponse
            {
                Created = ((DateTimeOffset)prediction.CreatedAt).ToUnixTimeSeconds(),
                Data = videoUrls.Select(url => new VideoData
                {
                    Url = url
                }).ToList()
            };
        }
    }
}