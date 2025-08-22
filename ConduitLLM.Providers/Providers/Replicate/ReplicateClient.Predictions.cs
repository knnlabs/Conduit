using System.Net.Http.Json;
using System.Text.Json;

using ConduitLLM.Core.Exceptions;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Replicate
{
    public partial class ReplicateClient
    {
        /// <summary>
        /// Cancels a running prediction on Replicate.
        /// </summary>
        private async Task CancelPredictionAsync(string predictionId, string? apiKey)
        {
            try
            {
                using var client = CreateHttpClient(apiKey);
                var response = await client.PostAsync($"predictions/{predictionId}/cancel", null);
                
                if (response.IsSuccessStatusCode)
                {
                    Logger.LogInformation("Successfully cancelled Replicate prediction {Id}", predictionId);
                }
                else
                {
                    Logger.LogWarning("Failed to cancel Replicate prediction {Id}: {StatusCode}", 
                        predictionId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error cancelling Replicate prediction {Id}", predictionId);
            }
        }

        private async Task<ReplicatePredictionResponse> StartPredictionAsync(
            ReplicatePredictionRequest request,
            string? apiKey,
            CancellationToken cancellationToken)
        {
            try
            {
                using var client = CreateHttpClient(apiKey);
                
                // Determine the endpoint based on the model ID format
                string endpoint;
                if (ProviderModelId.Contains('/') && !ProviderModelId.Contains(':'))
                {
                    // Model slug format (e.g., "bytedance/seedream-3")
                    // Use the models/{owner}/{name}/predictions endpoint (relative path)
                    endpoint = $"models/{ProviderModelId}/predictions";
                    // Remove the version field from the request since we're using the model endpoint
                    request.Version = null;
                    Logger.LogInformation("Using model endpoint: {Endpoint} for model {ModelId}", endpoint, ProviderModelId);
                }
                else
                {
                    // Version hash format (e.g., "a1b2c3...")
                    // Use the predictions endpoint with version in body (relative path)
                    endpoint = "predictions";
                    Logger.LogInformation("Using version endpoint: {Endpoint} with version {Version}", endpoint, request.Version);
                }
                
                Logger.LogInformation("Sending request to Replicate: {BaseUrl}{Endpoint}", client.BaseAddress, endpoint);
                var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("Replicate API prediction creation failed with status code {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"Replicate API prediction creation failed with status code {response.StatusCode}. Response: {errorContent}");
                }

                var predictionResponse = await response.Content.ReadFromJsonAsync<ReplicatePredictionResponse>(
                    cancellationToken: cancellationToken);

                if (predictionResponse == null)
                {
                    throw new LLMCommunicationException("Failed to deserialize Replicate prediction response");
                }

                return predictionResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "HTTP request error communicating with Replicate API");
                throw new LLMCommunicationException($"HTTP request error communicating with Replicate API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex, "JSON error processing Replicate response");
                throw new LLMCommunicationException("Error deserializing Replicate response", ex);
            }
            catch (LLMCommunicationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unexpected error occurred while starting Replicate prediction");
                throw new LLMCommunicationException($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        private async Task<ReplicatePredictionResponse> PollPredictionUntilCompletedAsync(
            string predictionId,
            string? apiKey,
            CancellationToken cancellationToken,
            bool yieldProgress = false)
        {
            var startTime = DateTime.UtcNow;
            var attemptCount = 0;
            ReplicatePredictionResponse? prediction = null;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogInformation("Prediction polling was canceled");
                    throw new OperationCanceledException("Prediction polling was canceled", cancellationToken);
                }

                // Check if we've exceeded the maximum polling duration
                if (DateTime.UtcNow - startTime > MaxPollingDuration)
                {
                    Logger.LogError("Exceeded maximum polling duration for prediction {PredictionId}", predictionId);
                    throw new LLMCommunicationException($"Exceeded maximum polling duration for prediction {predictionId}");
                }

                attemptCount++;
                Logger.LogDebug("Polling prediction {PredictionId}, attempt {AttemptCount}", predictionId, attemptCount);

                try
                {
                    using var client = CreateHttpClient(apiKey);
                    var response = await client.GetAsync($"predictions/{predictionId}", cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                        Logger.LogError("Replicate API prediction polling failed with status code {StatusCode}. Response: {ErrorContent}",
                            response.StatusCode, errorContent);
                        throw new LLMCommunicationException(
                            $"Replicate API prediction polling failed with status code {response.StatusCode}. Response: {errorContent}");
                    }

                    prediction = await response.Content.ReadFromJsonAsync<ReplicatePredictionResponse>(
                        cancellationToken: cancellationToken);

                    if (prediction == null)
                    {
                        throw new LLMCommunicationException("Failed to deserialize Replicate prediction response");
                    }

                    // Check prediction status
                    switch (prediction.Status.ToLowerInvariant())
                    {
                        case "succeeded":
                            Logger.LogInformation("Prediction {PredictionId} completed successfully", predictionId);
                            return prediction;

                        case "failed":
                            Logger.LogError("Prediction {PredictionId} failed: {Error}", predictionId, prediction.Error);
                            throw new LLMCommunicationException($"Replicate prediction failed: {prediction.Error}");

                        case "canceled":
                            Logger.LogWarning("Prediction {PredictionId} was canceled", predictionId);
                            throw new LLMCommunicationException("Replicate prediction was canceled");

                        case "starting":
                        case "processing":
                            // Still in progress, continue polling
                            Logger.LogDebug("Prediction {PredictionId} is {Status}", predictionId, prediction.Status);
                            break;

                        default:
                            Logger.LogWarning("Prediction {PredictionId} has unknown status: {Status}", predictionId, prediction.Status);
                            break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Logger.LogError(ex, "HTTP request error during prediction polling");
                    throw new LLMCommunicationException($"HTTP request error during prediction polling: {ex.Message}", ex);
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, "JSON error processing prediction polling response");
                    throw new LLMCommunicationException("Error deserializing prediction polling response", ex);
                }
                catch (LLMCommunicationException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An unexpected error occurred during prediction polling");
                    throw new LLMCommunicationException($"An unexpected error occurred during prediction polling: {ex.Message}", ex);
                }

                // Add a delay before the next poll
                await Task.Delay(DefaultPollingInterval, cancellationToken);
            }
        }
    }
}