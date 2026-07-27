using System.Diagnostics;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.UsageTracking;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;


namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Images controller - Synchronous image generation
    /// </summary>
    public partial class ImagesEndpoints
    {
        /// <summary>
        /// Creates one or more images given a prompt.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <param name="cancellationToken">Cancellation token from the HTTP request.</param>
        /// <returns>Generated images.</returns>
        public async Task<IResult> CreateImage(
            ConduitLLM.Core.Models.ImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            ConduitLLM.Configuration.Entities.ModelProviderMapping? mapping = null;
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return OpenAIError(400, "Prompt is required", "missing_parameter", "invalid_request_error", "prompt");
                }

                var modelName = request.Model ?? "dall-e-2";
                request.Model = modelName;
                var accounting = HttpContext.GetOrCreateRequestAccountingContext();
                accounting.SetOperation(RequestOperation.Image, CurrentVirtualKeyId, modelName);

                // Store image request details for usage tracking.
                // Set before mapping lookup since request.Model may be updated to the provider model ID later.
                HttpContext.SetUsageContext(new ImageUsageContext
                {
                    Model = modelName,
                    Quality = request.Quality,
                    Size = request.Size,
                    N = request.N,
                    Style = request.Style
                });

                // First check model mappings for image generation capability
                mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                bool supportsImageGen = false;
                
                if (mapping != null)
                {
                    // Check if the mapping indicates image generation support
                    supportsImageGen = mapping.ModelProviderTypeAssociation?.Model?.SupportsImageGeneration ?? false;

                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}",
                        modelName, supportsImageGen);

                    // Store provider info for usage tracking
                    HttpContext.Items["ProviderId"] = mapping.ProviderId;
                    HttpContext.Items["ProviderType"] = mapping.Provider?.ProviderType;

                    // Store ModelCostId for direct cost lookup (preferred over string matching)
                    if (mapping.ModelProviderTypeAssociation?.ModelCostId != null)
                    {
                        HttpContext.Items[HttpContextKeys.ModelCostId] = mapping.ModelProviderTypeAssociation.ModelCostId;
                    }
                }
                else
                {
                    // Model must be mapped to be used
                    _logger.LogWarning("No mapping found for model {Model}. Model must be configured in model mappings.", modelName);
                    supportsImageGen = false;
                }
                
                if (!supportsImageGen)
                {
                    return OpenAIError(400, $"Model {modelName} does not support image generation", "unsupported_model", "invalid_request_error", "model");
                }

                // If we don't have a mapping, try to create a client anyway (for direct model names)
                if (mapping == null)
                {
                    _logger.LogWarning("No provider mapping found for model {Model}, attempting direct client creation", modelName);
                }

                // Create client for the model
                var client = await _clientFactory.GetClientAsync(modelName);
                
                // Update request with the provider's model ID if we have a mapping
                if (mapping != null)
                {
                    request.Model = mapping.ProviderModelId;
                }
                
                // Generate images
                var response = await client.CreateImageAsync(request, cancellationToken: cancellationToken);
                var imageUsage = response.Usage ?? new Usage();
                imageUsage.ImageCount ??= response.Data.Count;
                imageUsage.ImageQuality ??= request.Quality;
                imageUsage.ImageResolution ??= request.Size;
                accounting.RecordProviderUsage(
                    imageUsage,
                    modelName,
                    response.Usage is null ? UsageEvidenceSource.Estimated : UsageEvidenceSource.Provider);

                await StoreGeneratedImagesAsync(response, request, mapping, cancellationToken);

                GatewayOpsMetrics.RecordMediaOperation("generate", "image", "success", sw.Elapsed.TotalSeconds, request.Model);
                return Ok(response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Image generation failed for model {Model}: {ErrorType} - {Message}",
                    request.Model, ex.GetType().Name, ex.Message);
                GatewayOpsMetrics.RecordMediaOperation("generate", "image", "error", sw.Elapsed.TotalSeconds, request.Model);

                // Track error in the provider error system for dashboard visibility and auto-disable
                var keyCredentialId = mapping?.Provider?.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary)?.Id
                    ?? mapping?.Provider?.ProviderKeyCredentials?.FirstOrDefault()?.Id;
                await TrackProviderErrorAsync(ex, request.Model, mapping?.ProviderId, keyCredentialId);

                // Rethrow — OpenAIErrorMiddleware maps exceptions to proper HTTP responses
                // via ExceptionToResponseMapper (e.g., 429 for RateLimitExceeded, 408 for Timeout, etc.)
                throw;
            }
        }

        private async Task StoreGeneratedImagesAsync(
            ImageGenerationResponse response,
            ImageGenerationRequest request,
            ConduitLLM.Configuration.Entities.ModelProviderMapping? mapping,
            CancellationToken cancellationToken)
        {
            var modelInfo = new GenerationModelInfo
            {
                ModelId = request.Model ?? "unknown",
                ModelAlias = mapping?.ModelAlias ?? request.Model ?? "unknown",
                ProviderId = mapping?.ProviderId ?? 0,
                Provider = mapping?.Provider,
                ModelCostId = mapping?.ModelProviderTypeAssociation?.ModelCostId
            };

            for (var index = 0; index < response.Data.Count; index++)
            {
                var imageData = response.Data[index];
                if (string.IsNullOrEmpty(imageData.B64Json) && string.IsNullOrEmpty(imageData.Url))
                {
                    _logger.LogWarning("Image data has neither URL nor base64 content at index {Index}", index);
                    continue;
                }

                var context = new MediaProcessingContext
                {
                    MediaType = MediaType.Image,
                    Index = index,
                    ModelInfo = modelInfo,
                    Prompt = request.Prompt,
                    VirtualKeyId = CurrentVirtualKeyId ?? 0,
                    CreatedBy = request.User,
                    RequestId = HttpContext.TraceIdentifier,
                    CorrelationId = HttpContext.TraceIdentifier
                };

                ProcessedMediaItem processed;
                try
                {
                    processed = !string.IsNullOrEmpty(imageData.B64Json)
                        ? await _base64MediaProcessor.ProcessAsync(imageData, context, cancellationToken)
                        : await _urlMediaProcessor.ProcessAsync(imageData, context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Failed to store generated image at index {Index}", index);
                    continue;
                }

                imageData.Url = processed.Url;
                if (!string.IsNullOrEmpty(processed.StorageKey))
                {
                    await TrackStoredImageAsync(processed, request, mapping);
                }

                if (request.ResponseFormat == "b64_json" && !string.IsNullOrEmpty(processed.StorageKey))
                {
                    using var storedStream = await _storageService.GetStreamAsync(processed.StorageKey);
                    if (storedStream is not null)
                    {
                        using var buffer = new MemoryStream();
                        await storedStream.CopyToAsync(buffer, cancellationToken);
                        imageData.B64Json = Convert.ToBase64String(buffer.ToArray());
                    }
                    imageData.Url = null;
                }
                else if (request.ResponseFormat == "url")
                {
                    imageData.B64Json = null;
                }
            }
        }

        private async Task TrackStoredImageAsync(
            ProcessedMediaItem processed,
            ImageGenerationRequest request,
            ConduitLLM.Configuration.Entities.ModelProviderMapping? mapping)
        {
            if (CurrentVirtualKeyId is not int virtualKeyId || string.IsNullOrEmpty(processed.StorageKey))
            {
                _logger.LogWarning("Could not determine virtual key ID for media tracking");
                return;
            }

            try
            {
                var info = await _storageService.GetInfoAsync(processed.StorageKey);
                await _mediaLifecycleService.TrackMediaAsync(
                    virtualKeyId,
                    processed.StorageKey,
                    "image",
                    new Core.Interfaces.MediaLifecycleMetadata
                    {
                        ContentType = info?.ContentType ?? "image/png",
                        SizeBytes = info?.SizeBytes ?? 0,
                        Provider = mapping?.Provider?.ProviderType.ToString() ?? "unknown",
                        Model = request.Model ?? "unknown",
                        Prompt = request.Prompt,
                        StorageUrl = processed.Url,
                        PublicUrl = processed.Url
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to track media ownership for {StorageKey}, but continuing with response",
                    processed.StorageKey);
            }
        }

        /// <summary>
        /// Classifies an exception and tracks it in the provider error system.
        /// </summary>
        private async Task TrackProviderErrorAsync(Exception ex, string? modelName, int? providerId, int? keyCredentialId)
        {
            try
            {
                if (providerId == null || keyCredentialId == null)
                {
                    _logger.LogWarning("Cannot track provider error — missing provider context (ProviderId={ProviderId}, KeyCredentialId={KeyCredentialId})",
                        providerId, keyCredentialId);
                    return;
                }

                var errorType = ConduitLLM.Core.Models.ProviderErrorClassifier.ClassifyException(ex);
                int? httpStatusCode = (ex as LLMCommunicationException)?.StatusCode.HasValue == true
                    ? (int)(ex as LLMCommunicationException)!.StatusCode!.Value
                    : null;

                var errorInfo = new ConduitLLM.Core.Models.ProviderErrorInfo
                {
                    KeyCredentialId = keyCredentialId.Value,
                    ProviderId = providerId.Value,
                    ErrorType = errorType,
                    ErrorMessage = ex.Message,
                    HttpStatusCode = httpStatusCode,
                    ModelName = modelName,
                    OccurredAt = DateTime.UtcNow,
                    RequestId = HttpContext.TraceIdentifier
                };

                await _errorTrackingService.TrackErrorAsync(errorInfo);

                _logger.LogInformation("Tracked provider error: Type={ErrorType}, Provider={ProviderId}, Key={KeyCredentialId}, Model={Model}",
                    errorType, providerId, keyCredentialId, modelName);
            }
            catch (Exception trackEx)
            {
                // Never let error tracking prevent the original error from propagating
                _logger.LogWarning(trackEx, "Failed to track provider error for model {Model}", modelName);
            }
        }

    }
}
