using System.Net;
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
                using var response = await client.PostAsync($"predictions/{predictionId}/cancel", null);
                
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
                using var response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                    Logger.LogError("Replicate API prediction creation failed with status {StatusCode}. Response: {ErrorContent}",
                        response.StatusCode, errorContent);
                    throw new LLMCommunicationException(
                        $"Replicate prediction creation failed: {errorContent}",
                        response.StatusCode, errorContent);
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
            catch (ConduitException)
            {
                throw;
            }
            catch (OperationCanceledException)
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
            string? lastStatus = null;
            ReplicatePredictionResponse? prediction = null;

            Logger.LogInformation("Starting to poll prediction {PredictionId}, max duration: {MaxDuration}",
                predictionId, MaxPollingDuration);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    Logger.LogWarning("Prediction {PredictionId} polling canceled after {ElapsedSeconds:F1}s and {AttemptCount} attempts",
                        predictionId, elapsed.TotalSeconds, attemptCount);

                    // Best-effort cancel on Replicate side
                    _ = CancelPredictionAsync(predictionId, apiKey);
                    throw new OperationCanceledException("Prediction polling was canceled", cancellationToken);
                }

                // Check if we've exceeded the maximum polling duration
                var duration = DateTime.UtcNow - startTime;
                if (duration > MaxPollingDuration)
                {
                    Logger.LogError("Prediction {PredictionId} timed out after {ElapsedSeconds:F1}s and {AttemptCount} attempts. Last status: {LastStatus}",
                        predictionId, duration.TotalSeconds, attemptCount, lastStatus ?? "unknown");

                    // Best-effort cancel on Replicate side
                    _ = CancelPredictionAsync(predictionId, apiKey);
                    throw new RequestTimeoutException(
                        $"Replicate prediction {predictionId} timed out after {MaxPollingDuration.TotalSeconds:F0}s (last status: {lastStatus ?? "unknown"})",
                        (int)MaxPollingDuration.TotalSeconds,
                        "replicate_prediction_polling");
                }

                attemptCount++;

                try
                {
                    using var client = CreateHttpClient(apiKey);
                    using var response = await client.GetAsync($"predictions/{predictionId}", cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await ReadErrorContentAsync(response, cancellationToken);
                        Logger.LogError("Replicate API prediction polling failed with status {StatusCode} after {AttemptCount} attempts. Response: {ErrorContent}",
                            response.StatusCode, attemptCount, errorContent);
                        throw new LLMCommunicationException(
                            $"Replicate prediction polling failed: {errorContent}",
                            response.StatusCode, errorContent);
                    }

                    prediction = await response.Content.ReadFromJsonAsync<ReplicatePredictionResponse>(
                        cancellationToken: cancellationToken);

                    if (prediction == null)
                    {
                        throw new LLMCommunicationException("Failed to deserialize Replicate prediction response");
                    }

                    var currentStatus = prediction.Status.ToLowerInvariant();

                    // Log status changes and periodic updates
                    if (currentStatus != lastStatus)
                    {
                        Logger.LogInformation("Prediction {PredictionId} status changed: {OldStatus} → {NewStatus} (attempt {AttemptCount}, elapsed {ElapsedSeconds:F1}s)",
                            predictionId, lastStatus ?? "initial", currentStatus, attemptCount, duration.TotalSeconds);
                        lastStatus = currentStatus;
                    }
                    else if (attemptCount % 15 == 0)
                    {
                        // Log every 15th poll (~30s) as a heartbeat
                        Logger.LogInformation("Prediction {PredictionId} still {Status} after {AttemptCount} attempts ({ElapsedSeconds:F1}s)",
                            predictionId, currentStatus, attemptCount, duration.TotalSeconds);
                    }

                    // Check prediction status
                    switch (currentStatus)
                    {
                        case "succeeded":
                            Logger.LogInformation("Prediction {PredictionId} completed successfully after {AttemptCount} attempts ({ElapsedSeconds:F1}s)",
                                predictionId, attemptCount, duration.TotalSeconds);
                            return prediction;

                        case "failed":
                            Logger.LogError("Prediction {PredictionId} failed after {AttemptCount} attempts ({ElapsedSeconds:F1}s): {Error}",
                                predictionId, attemptCount, duration.TotalSeconds, prediction.Error);
                            throw ClassifyReplicatePredictionError(prediction.Error, predictionId);

                        case "canceled":
                            Logger.LogWarning("Prediction {PredictionId} was canceled by Replicate after {AttemptCount} attempts ({ElapsedSeconds:F1}s)",
                                predictionId, attemptCount, duration.TotalSeconds);
                            throw new LLMCommunicationException("Replicate prediction was canceled");

                        case "starting":
                        case "processing":
                            // Still in progress, continue polling
                            break;

                        default:
                            Logger.LogWarning("Prediction {PredictionId} has unknown status: {Status}", predictionId, currentStatus);
                            break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    Logger.LogError(ex, "Network error polling prediction {PredictionId} on attempt {AttemptCount}",
                        predictionId, attemptCount);
                    throw new LLMCommunicationException(
                        $"Network error polling Replicate prediction: {ex.Message}",
                        HttpStatusCode.BadGateway, null, ex);
                }
                catch (JsonException ex)
                {
                    Logger.LogError(ex, "JSON error processing prediction {PredictionId} polling response on attempt {AttemptCount}",
                        predictionId, attemptCount);
                    throw new LLMCommunicationException("Error deserializing prediction polling response", ex);
                }
                catch (LLMCommunicationException)
                {
                    throw;
                }
                catch (RequestTimeoutException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (RateLimitExceededException)
                {
                    throw;
                }
                catch (ServiceUnavailableException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unexpected error polling prediction {PredictionId} on attempt {AttemptCount}",
                        predictionId, attemptCount);
                    throw new LLMCommunicationException($"Unexpected error during prediction polling: {ex.Message}", ex);
                }

                // Add a delay before the next poll
                await Task.Delay(DefaultPollingInterval, cancellationToken);
            }
        }

        /// <summary>
        /// Classifies a Replicate prediction error into the appropriate exception type
        /// based on the error message content.
        /// </summary>
        private Exception ClassifyReplicatePredictionError(string? error, string predictionId)
        {
            if (string.IsNullOrEmpty(error))
            {
                return new LLMCommunicationException($"Replicate prediction {predictionId} failed with no error details");
            }

            var errorLower = error.ToLowerInvariant();

            // Authentication / authorization errors
            if (errorLower.Contains("invalid api token") || errorLower.Contains("unauthorized") ||
                errorLower.Contains("authentication") || errorLower.Contains("invalid token"))
            {
                return new LLMCommunicationException(
                    $"Replicate authentication error: {error}",
                    HttpStatusCode.Unauthorized, error);
            }

            // Billing / quota errors
            if (errorLower.Contains("insufficient") || errorLower.Contains("billing") ||
                errorLower.Contains("payment") || errorLower.Contains("quota") ||
                errorLower.Contains("credit"))
            {
                return new LLMCommunicationException(
                    $"Replicate billing error: {error}",
                    HttpStatusCode.PaymentRequired, error);
            }

            // Rate limiting
            if (errorLower.Contains("rate limit") || errorLower.Contains("too many requests") ||
                errorLower.Contains("throttl"))
            {
                return new RateLimitExceededException($"Replicate rate limit: {error}");
            }

            // Content policy
            if (errorLower.Contains("nsfw") || errorLower.Contains("content policy") ||
                errorLower.Contains("safety") || errorLower.Contains("moderation") ||
                errorLower.Contains("not allowed"))
            {
                return new InvalidRequestException($"Content policy violation: {error}", "content_policy_violation", "prompt");
            }

            // Model errors
            if (errorLower.Contains("model") && (errorLower.Contains("not found") || errorLower.Contains("does not exist")))
            {
                return new ModelNotFoundException(ProviderModelId, $"Replicate model error: {error}");
            }

            // Input validation
            if (errorLower.Contains("invalid input") || errorLower.Contains("validation") ||
                errorLower.Contains("invalid value") || errorLower.Contains("must be"))
            {
                return new InvalidRequestException($"Replicate input validation error: {error}");
            }

            // Service errors
            if (errorLower.Contains("service unavailable") || errorLower.Contains("internal error") ||
                errorLower.Contains("server error"))
            {
                return new ServiceUnavailableException($"Replicate service error: {error}");
            }

            // Default: unclassified provider error with the original message preserved
            return new LLMCommunicationException($"Replicate prediction failed: {error}");
        }
    }
}