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

            Logger.LogInformation("Creating video with Replicate for model '{ModelId}' with prompt: '{Prompt}'", 
                ProviderModelId, request.Prompt);

            try
            {
                // Map the request to Replicate format and start prediction
                var predictionRequest = MapToVideoGenerationRequest(request);
                
                Logger.LogDebug("Video generation request mapped. Input parameters: {@InputParams}", predictionRequest.Input);
                
                var predictionResponse = await StartPredictionAsync(predictionRequest, apiKey, cancellationToken);
                
                Logger.LogInformation("Video generation prediction started with ID: {PredictionId}, Status: {Status}", 
                    predictionResponse.Id, predictionResponse.Status);

                // Poll until prediction completes or fails
                var finalPrediction = await PollPredictionUntilCompletedAsync(predictionResponse.Id, apiKey, cancellationToken);

                Logger.LogInformation("Video generation completed for prediction {PredictionId}. Final status: {Status}, Output: {@Output}", 
                    finalPrediction.Id, finalPrediction.Status, finalPrediction.Output);

                // Process the final result
                return MapToVideoGenerationResponse(finalPrediction, request.Model);
            }
            catch (LLMCommunicationException ex)
            {
                Logger.LogError(ex, "Video generation failed with LLMCommunicationException for model {ModelId}, prompt: '{Prompt}'", 
                    ProviderModelId, request.Prompt);
                // Re-throw LLMCommunicationException directly
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while processing Replicate video generation for model {ModelId}, prompt: '{Prompt}'", 
                    ProviderModelId, request.Prompt);
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

            // Log parameters being sent (excluding prompt content)
            var logParams = new Dictionary<string, object>(input);
            logParams["prompt"] = $"[REDACTED: {request.Prompt.Length} chars]";
            
            Logger.LogInformation("Image generation parameters for Replicate: Model={Model}, Parameters={@Parameters}", 
                ProviderModelId, logParams);

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

            // Log parameters being sent (excluding prompt content)
            var logParams = new Dictionary<string, object>(input);
            logParams["prompt"] = $"[REDACTED: {request.Prompt.Length} chars]";
            
            Logger.LogInformation("Video generation parameters for Replicate: Model={Model}, Parameters={@Parameters}", 
                ProviderModelId, logParams);

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
            Logger.LogDebug("Mapping prediction response to VideoGenerationResponse. Prediction ID: {Id}, Status: {Status}", 
                prediction.Id, prediction.Status);
            
            // Extract video URLs from the prediction output
            var videoUrls = ExtractVideoUrlsFromPredictionOutput(prediction.Output);
            
            if (videoUrls.Count == 0)
            {
                Logger.LogError("Failed to extract any video URLs from prediction {Id} with status {Status}. Output: {@Output}", 
                    prediction.Id, prediction.Status, prediction.Output);
                throw new LLMCommunicationException($"No video URLs found in Replicate prediction output for prediction {prediction.Id}");
            }

            var response = new VideoGenerationResponse
            {
                Created = ((DateTimeOffset)prediction.CreatedAt).ToUnixTimeSeconds(),
                Data = videoUrls.Select(url => new VideoData
                {
                    Url = url
                }).ToList()
            };
            
            Logger.LogInformation("Successfully mapped video generation response with {Count} video(s) for prediction {Id}", 
                response.Data.Count, prediction.Id);
            
            return response;
        }
    }
}