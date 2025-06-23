using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for image generation using the Core API.
/// </summary>
public class ImagesService
{
    private readonly BaseClient _client;
    private readonly ILogger<ImagesService>? _logger;
    private const string GenerationsEndpoint = "/v1/images/generations";
    private const string AsyncGenerationsEndpoint = "/v1/images/generations/async";
    private const string EditsEndpoint = "/v1/images/edits";
    private const string VariationsEndpoint = "/v1/images/variations";

    /// <summary>
    /// Initializes a new instance of the ImagesService class.
    /// </summary>
    /// <param name="client">The base client to use for HTTP requests.</param>
    /// <param name="logger">Optional logger instance.</param>
    public ImagesService(BaseClient client, ILogger<ImagesService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    /// <summary>
    /// Generates images from a text prompt.
    /// </summary>
    /// <param name="request">The image generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated images.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<ImageGenerationResponse> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateGenerationRequest(request);
            
            _logger?.LogDebug("Generating {Count} image(s) with model {Model} for prompt: {Prompt}", 
                request.N ?? 1, request.Model ?? ImageModels.DallE3, request.Prompt);

            // Convert enums to string values for API
            var apiRequest = ConvertToApiRequest(request);

            var response = await _client.PostForServiceAsync<ImageGenerationResponse>(
                GenerationsEndpoint,
                apiRequest,
                cancellationToken);

            _logger?.LogDebug("Generated {Count} image(s) successfully", response.Data?.Count() ?? 0);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Edits an existing image using a prompt and optional mask.
    /// </summary>
    /// <param name="request">The image edit request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The edited images.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<ImageGenerationResponse> EditAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateEditRequest(request);
            
            _logger?.LogDebug("Editing image with prompt: {Prompt}", request.Prompt);

            // Create multipart form data content
            using var content = new MultipartFormDataContent();
            
            // Add image
            var imageContent = new StreamContent(request.Image);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", request.ImageFileName);

            // Add prompt
            content.Add(new StringContent(request.Prompt), "prompt");

            // Add mask if provided
            if (request.Mask != null)
            {
                var maskContent = new StreamContent(request.Mask);
                maskContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                content.Add(maskContent, "mask", request.MaskFileName ?? "mask.png");
            }

            // Add optional parameters
            AddOptionalParameters(content, request);

            var response = await _client.HttpClientForServices.PostAsync(EditsEndpoint, content, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response);

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = System.Text.Json.JsonSerializer.Deserialize<ImageGenerationResponse>(
                jsonResponse, _client.JsonSerializerOptionsForServices);

            _logger?.LogDebug("Edited image successfully, generated {Count} result(s)", result?.Data?.Count() ?? 0);
            return result ?? new ImageGenerationResponse();
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Creates variations of an existing image.
    /// </summary>
    /// <param name="request">The image variation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The image variations.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<ImageGenerationResponse> CreateVariationsAsync(
        ImageVariationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateVariationRequest(request);
            
            _logger?.LogDebug("Creating {Count} variation(s) of image", request.N ?? 1);

            // Create multipart form data content
            using var content = new MultipartFormDataContent();
            
            // Add image
            var imageContent = new StreamContent(request.Image);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", request.ImageFileName);

            // Add optional parameters
            AddOptionalVariationParameters(content, request);

            var response = await _client.HttpClientForServices.PostAsync(VariationsEndpoint, content, cancellationToken);
            await ErrorHandler.HandleErrorResponseAsync(response);

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = System.Text.Json.JsonSerializer.Deserialize<ImageGenerationResponse>(
                jsonResponse, _client.JsonSerializerOptionsForServices);

            _logger?.LogDebug("Created image variations successfully, generated {Count} result(s)", result?.Data?.Count() ?? 0);
            return result ?? new ImageGenerationResponse();
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Validates that a model supports image generation with the specified parameters.
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <param name="size">The requested image size.</param>
    /// <param name="quality">The requested image quality.</param>
    /// <param name="style">The requested image style.</param>
    /// <param name="n">The number of images requested.</param>
    /// <returns>True if the model supports the parameters, false otherwise.</returns>
    public bool ValidateModelCapabilities(
        string model,
        ImageSize? size = null,
        ImageQuality? quality = null,
        ImageStyle? style = null,
        int? n = null)
    {
        var capabilities = ImageModelCapabilities.GetCapabilities(model);
        if (capabilities == null)
            return false;

        // Check size support
        if (size.HasValue && !capabilities.SupportedSizes.Contains(size.Value))
            return false;

        // Check quality support
        if (quality.HasValue && !capabilities.SupportedQualities.Contains(quality.Value))
            return false;

        // Check style support
        if (style.HasValue && !capabilities.SupportedStyles.Contains(style.Value))
            return false;

        // Check number of images
        if (n.HasValue && n.Value > capabilities.MaxImages)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the capabilities for a specific image model.
    /// </summary>
    /// <param name="model">The model name.</param>
    /// <returns>The model capabilities, or null if the model is not supported.</returns>
    public ImageModelCapability? GetModelCapabilities(string model)
    {
        return ImageModelCapabilities.GetCapabilities(model);
    }

    /// <summary>
    /// Generates images asynchronously from a text prompt.
    /// </summary>
    /// <param name="request">The async image generation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The async task information.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<AsyncImageGenerationResponse> GenerateAsync(
        AsyncImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateAsyncGenerationRequest(request);
            
            _logger?.LogDebug("Starting async generation of {Count} image(s) with model {Model} for prompt: {Prompt}", 
                request.N ?? 1, request.Model ?? ImageModels.DallE3, request.Prompt);

            // Convert to API request format
            var apiRequest = ConvertToAsyncApiRequest(request);

            var response = await _client.PostForServiceAsync<AsyncImageGenerationResponse>(
                AsyncGenerationsEndpoint,
                apiRequest,
                cancellationToken);

            _logger?.LogDebug("Async image generation task created with ID: {TaskId}", response.TaskId);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the status of an async image generation task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current task status and result if completed.</returns>
    /// <exception cref="ValidationException">Thrown when the task ID is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<AsyncImageGenerationResponse> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ValidationException("Task ID is required", "taskId");

            var endpoint = $"{AsyncGenerationsEndpoint}/{Uri.EscapeDataString(taskId)}/status";
            
            var response = await _client.GetForServiceAsync<AsyncImageGenerationResponse>(
                endpoint,
                cancellationToken);

            _logger?.LogDebug("Retrieved status for task {TaskId}: {Status} ({Progress}%)", 
                taskId, response.Status, response.Progress);
            
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Cancels a pending or running async image generation task.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ValidationException">Thrown when the task ID is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ValidationException("Task ID is required", "taskId");

            var endpoint = $"{AsyncGenerationsEndpoint}/{Uri.EscapeDataString(taskId)}";
            
            await _client.HttpClientForServices.DeleteAsync(endpoint, cancellationToken);
            
            _logger?.LogDebug("Cancelled task {TaskId}", taskId);
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Polls an async image generation task until completion or timeout.
    /// </summary>
    /// <param name="taskId">The task identifier.</param>
    /// <param name="options">Polling options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final task result when completed.</returns>
    /// <exception cref="ValidationException">Thrown when parameters are invalid.</exception>
    /// <exception cref="TimeoutException">Thrown when polling times out.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails or task fails.</exception>
    public async Task<ImageGenerationResponse> PollTaskUntilCompletionAsync(
        string taskId,
        TaskPollingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new TaskPollingOptions();
        
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ValidationException("Task ID is required", "taskId");

        var startTime = DateTime.UtcNow;
        var currentInterval = options.IntervalMs;
        
        _logger?.LogDebug("Starting to poll task {TaskId} with interval {IntervalMs}ms, timeout {TimeoutMs}ms", 
            taskId, options.IntervalMs, options.TimeoutMs);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check timeout
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > options.TimeoutMs)
            {
                throw new TimeoutException($"Task polling timed out after {options.TimeoutMs}ms");
            }

            var status = await GetTaskStatusAsync(taskId, cancellationToken);

            switch (status.Status)
            {
                case ImageTaskStatus.Completed:
                    if (status.Result == null)
                        throw new ConduitCoreException("Task completed but no result was provided", null, null, null, null);
                    
                    _logger?.LogDebug("Task {TaskId} completed successfully", taskId);
                    return status.Result;

                case ImageTaskStatus.Failed:
                    throw new ConduitCoreException($"Task failed: {status.Error ?? "Unknown error"}", null, null, null, null);

                case ImageTaskStatus.Cancelled:
                    throw new ConduitCoreException("Task was cancelled", null, null, null, null);

                case ImageTaskStatus.TimedOut:
                    throw new ConduitCoreException("Task timed out", null, null, null, null);

                case ImageTaskStatus.Pending:
                case ImageTaskStatus.Running:
                    // Continue polling
                    break;

                default:
                    throw new ConduitCoreException($"Unknown task status: {status.Status}", null, null, null, null);
            }

            // Wait before next poll
            await Task.Delay(currentInterval, cancellationToken);

            // Apply exponential backoff if enabled
            if (options.UseExponentialBackoff)
            {
                currentInterval = Math.Min(currentInterval * 2, options.MaxIntervalMs);
            }
        }
    }

    private static void ValidateGenerationRequest(ImageGenerationRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ValidationException("Prompt is required", "prompt");

        if (!string.IsNullOrEmpty(request.Model))
        {
            var capabilities = ImageModelCapabilities.GetCapabilities(request.Model);
            if (capabilities == null)
                throw new ValidationException($"Unsupported model: {request.Model}", "model");

            if (request.Prompt.Length > capabilities.MaxPromptLength)
                throw new ValidationException($"Prompt too long for model {request.Model}. Maximum length: {capabilities.MaxPromptLength}", "prompt");

            if (request.N > capabilities.MaxImages)
                throw new ValidationException($"Too many images requested for model {request.Model}. Maximum: {capabilities.MaxImages}", "n");

            if (request.Size.HasValue && !capabilities.SupportedSizes.Contains(request.Size.Value))
                throw new ValidationException($"Unsupported size for model {request.Model}", "size");

            if (request.Quality.HasValue && !capabilities.SupportedQualities.Contains(request.Quality.Value))
                throw new ValidationException($"Unsupported quality for model {request.Model}", "quality");

            if (request.Style.HasValue && !capabilities.SupportedStyles.Contains(request.Style.Value))
                throw new ValidationException($"Unsupported style for model {request.Model}", "style");
        }

        if (request.N.HasValue && (request.N <= 0 || request.N > 10))
            throw new ValidationException("N must be between 1 and 10", "n");
    }

    private static void ValidateEditRequest(ImageEditRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Image == null)
            throw new ValidationException("Image is required", "image");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ValidationException("Prompt is required", "prompt");

        if (request.Prompt.Length > 1000)
            throw new ValidationException("Prompt too long. Maximum length: 1000", "prompt");

        if (request.N.HasValue && (request.N <= 0 || request.N > 10))
            throw new ValidationException("N must be between 1 and 10", "n");
    }

    private static void ValidateVariationRequest(ImageVariationRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (request.Image == null)
            throw new ValidationException("Image is required", "image");

        if (request.N.HasValue && (request.N <= 0 || request.N > 10))
            throw new ValidationException("N must be between 1 and 10", "n");
    }

    private static object ConvertToApiRequest(ImageGenerationRequest request)
    {
        return new
        {
            prompt = request.Prompt,
            model = request.Model,
            n = request.N,
            quality = request.Quality?.ToString().ToLower(),
            response_format = request.ResponseFormat?.ToString().ToLower(),
            size = ConvertSizeToString(request.Size),
            style = request.Style?.ToString().ToLower(),
            user = request.User
        };
    }

    private static string? ConvertSizeToString(ImageSize? size)
    {
        return size switch
        {
            ImageSize.Size256x256 => "256x256",
            ImageSize.Size512x512 => "512x512",
            ImageSize.Size1024x1024 => "1024x1024",
            ImageSize.Size1792x1024 => "1792x1024",
            ImageSize.Size1024x1792 => "1024x1792",
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unsupported image size")
        };
    }

    private static void AddOptionalParameters(MultipartFormDataContent content, ImageEditRequest request)
    {
        if (!string.IsNullOrEmpty(request.Model))
            content.Add(new StringContent(request.Model), "model");

        if (request.N.HasValue)
            content.Add(new StringContent(request.N.Value.ToString()), "n");

        if (request.ResponseFormat.HasValue)
            content.Add(new StringContent(request.ResponseFormat.Value.ToString().ToLower()), "response_format");

        if (request.Size.HasValue)
            content.Add(new StringContent(ConvertSizeToString(request.Size)!), "size");

        if (!string.IsNullOrEmpty(request.User))
            content.Add(new StringContent(request.User), "user");
    }

    private static void AddOptionalVariationParameters(MultipartFormDataContent content, ImageVariationRequest request)
    {
        if (!string.IsNullOrEmpty(request.Model))
            content.Add(new StringContent(request.Model), "model");

        if (request.N.HasValue)
            content.Add(new StringContent(request.N.Value.ToString()), "n");

        if (request.ResponseFormat.HasValue)
            content.Add(new StringContent(request.ResponseFormat.Value.ToString().ToLower()), "response_format");

        if (request.Size.HasValue)
            content.Add(new StringContent(ConvertSizeToString(request.Size)!), "size");

        if (!string.IsNullOrEmpty(request.User))
            content.Add(new StringContent(request.User), "user");
    }

    private static void ValidateAsyncGenerationRequest(AsyncImageGenerationRequest request)
    {
        // First validate the base image generation request
        ValidateGenerationRequest(request);

        // Additional validation for async-specific fields
        if (request.TimeoutSeconds.HasValue && (request.TimeoutSeconds <= 0 || request.TimeoutSeconds > 3600))
            throw new ValidationException("Timeout must be between 1 and 3600 seconds", "timeoutSeconds");

        if (!string.IsNullOrEmpty(request.WebhookUrl))
        {
            if (!Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ValidationException("WebhookUrl must be a valid HTTP or HTTPS URL", "webhookUrl");
            }
        }
    }

    private static object ConvertToAsyncApiRequest(AsyncImageGenerationRequest request)
    {
        var baseRequest = ConvertToApiRequest(request);
        
        // Convert to dictionary to add async-specific fields
        var asyncRequest = new Dictionary<string, object>();
        
        // Add all base properties
        foreach (var property in baseRequest.GetType().GetProperties())
        {
            var value = property.GetValue(baseRequest);
            if (value != null)
            {
                asyncRequest[property.Name] = value;
            }
        }

        // Add async-specific properties
        if (!string.IsNullOrEmpty(request.WebhookUrl))
            asyncRequest["webhook_url"] = request.WebhookUrl;

        if (request.WebhookMetadata != null && request.WebhookMetadata.Any())
            asyncRequest["webhook_metadata"] = request.WebhookMetadata;

        if (request.TimeoutSeconds.HasValue)
            asyncRequest["timeout_seconds"] = request.TimeoutSeconds.Value;

        return asyncRequest;
    }
}