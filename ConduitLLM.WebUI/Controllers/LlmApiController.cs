using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http; 
using Microsoft.AspNetCore.Http.Json; 
using ConduitLLM.Core.Interfaces; 
using ConduitLLM.Core.Models;    
using ConduitLLM.Core.Exceptions;
using System.Text; // Add for Encoding

namespace ConduitLLM.WebUI.Controllers;

/// <summary>
/// Provides an OpenAI-compatible API for LLM interactions.
/// </summary>
/// <remarks>
/// This controller implements endpoints compatible with the OpenAI API format,
/// facilitating drop-in replacement for applications that use OpenAI's API.
/// It supports chat completions, embeddings, and model listing.
/// 
/// All requests to these endpoints are validated by middleware that checks
/// for valid virtual keys with appropriate permissions.
/// </remarks>
[ApiController]
[Route("api/v1")]
public class LlmApiController : ControllerBase
{
    private readonly ILogger<LlmApiController> _logger;
    private readonly ILLMRouter _router;

    /// <summary>
    /// Initializes a new instance of the LlmApiController.
    /// </summary>
    /// <param name="logger">Logger for recording diagnostic information.</param>
    /// <param name="router">Router for directing requests to appropriate LLM providers.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger or router is null.</exception>
    public LlmApiController(
        ILogger<LlmApiController> logger,
        ILLMRouter router)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    /// <summary>
    /// Creates a chat completion via the OpenAI-compatible API.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A chat completion response or an appropriate error.</returns>
    /// <remarks>
    /// This endpoint is compatible with OpenAI's chat completion API and supports both
    /// streaming and non-streaming responses. The request is routed to the appropriate
    /// provider based on the model specified and the router's configuration.
    /// 
    /// For streaming responses, data is sent using Server-Sent Events (SSE) format.
    /// </remarks>
    /// <response code="200">Returns a chat completion response.</response>
    /// <response code="400">If the request body is invalid or malformed.</response>
    /// <response code="500">If an unexpected error occurs.</response>
    /// <response code="502">If there's an error communicating with the LLM provider.</response>
    /// <response code="503">If the requested model is unavailable.</response>
    [HttpPost("chat/completions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChatCompletions(CancellationToken cancellationToken)
    {
        // Parse and validate the request
        ChatCompletionRequest? request = await ParseRequestAsync<ChatCompletionRequest>(cancellationToken);
        if (request == null)
        {
            // ParseRequestAsync already set the appropriate error response
            return BadRequest(new { error = "Invalid request body." });
        }

        // Handle streaming and non-streaming requests differently
        if (request.Stream == true)
        {
            // For streaming, delegate to a specialized method that handles SSE
            await StreamChatCompletionsInternal(request, cancellationToken);
            
            // Since the response is handled within the streaming method, return nothing here
            return new EmptyResult();
        }
        else
        {
            // For non-streaming, process the request normally
            return await ProcessNonStreamingChatCompletionAsync(request, cancellationToken);
        }
    }
    
    /// <summary>
    /// Processes a non-streaming chat completion request.
    /// </summary>
    /// <param name="request">The validated chat completion request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An action result containing the chat completion response or an error.</returns>
    private async Task<IActionResult> ProcessNonStreamingChatCompletionAsync(
        ChatCompletionRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the response from the router
            var response = await _router.CreateChatCompletionAsync(
                request, 
                cancellationToken: cancellationToken);
            
            // Return the successful response
            return Ok(response);
        }
        catch (Exception ex)
        {
            // Handle various exception types with appropriate status codes
            return HandleChatCompletionException(ex, request.Model, cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles exceptions that occur during chat completion processing.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="model">The model that was requested.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An appropriate action result for the exception.</returns>
    private IActionResult HandleChatCompletionException(
        Exception ex, 
        string? model, 
        CancellationToken cancellationToken)
    {
        if (ex is ModelUnavailableException modelEx)
        {
            _logger.LogWarning(modelEx, "Model unavailable for chat completion request: {Model}", model);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = modelEx.Message });
        }
        else if (ex is LLMCommunicationException commEx)
        {
            _logger.LogError(commEx, "Communication error during chat completion for model {Model}", model);
            return StatusCode(StatusCodes.Status502BadGateway, 
                new { error = $"LLM provider communication error: {commEx.Message}" });
        }
        else if (ex is ConfigurationException configEx)
        {
            _logger.LogError(configEx, "Configuration error during chat completion for model {Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = $"Configuration error: {configEx.Message}" });
        }
        else if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Chat completion request cancelled by client.");
            return StatusCode(StatusCodes.Status400BadRequest, new { error = "Request cancelled." });
        }
        else
        {
            _logger.LogError(ex, "Unexpected error during chat completion for model {Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An unexpected error occurred." });
        }
    }
    
    /// <summary>
    /// Parses and validates a request body.
    /// </summary>
    /// <typeparam name="T">The type of request to parse.</typeparam>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The parsed request, or null if parsing failed.</returns>
    private async Task<T?> ParseRequestAsync<T>(CancellationToken cancellationToken) where T : class
    {
        try
        {
            // Parse the request body
            var request = await Request.ReadFromJsonAsync<T>(cancellationToken);
            if (request == null)
            {
                _logger.LogWarning("Request body is null or empty for {RequestType}", typeof(T).Name);
                return null;
            }
            
            return request;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogWarning(jsonEx, "Failed to deserialize {RequestType}", typeof(T).Name);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading request body for {RequestType}", typeof(T).Name);
            return null;
        }
    }

    /// <summary>
    /// Handles streaming chat completion responses using Server-Sent Events (SSE).
    /// </summary>
    /// <param name="request">The chat completion request to process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the streaming operation.</returns>
    /// <remarks>
    /// This method is called by the ChatCompletions endpoint when streaming is requested.
    /// It sets up the response headers for SSE, requests a streaming response from the router,
    /// and forwards each chunk to the client as an SSE message.
    /// 
    /// The method handles various error scenarios, including:
    /// - Model unavailability
    /// - Communication errors with LLM providers
    /// - Configuration errors
    /// - Client cancellation
    /// 
    /// Each chunk is serialized to JSON and sent with the "data:" prefix as per SSE specification.
    /// When the stream completes normally, a final "data: [DONE]" message is sent.
    /// </remarks>
    /// <summary>
    /// Handles streaming chat completion responses using Server-Sent Events (SSE).
    /// </summary>
    /// <param name="request">The chat completion request to process.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the streaming operation.</returns>
    /// <remarks>
    /// This method is called by the ChatCompletions endpoint when streaming is requested.
    /// It sets up the response headers for SSE, requests a streaming response from the router,
    /// and forwards each chunk to the client as an SSE message.
    /// 
    /// The method handles various error scenarios, including:
    /// - Model unavailability
    /// - Communication errors with LLM providers
    /// - Configuration errors
    /// - Client cancellation
    /// 
    /// Each chunk is serialized to JSON and sent with the "data:" prefix as per SSE specification.
    /// When the stream completes normally, a final "data: [DONE]" message is sent.
    /// </remarks>
    private async Task StreamChatCompletionsInternal(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to stream chat completion for model {Model}", request.Model);
        ConfigureResponseForSSE();
        
        // Get the stream from the router
        IAsyncEnumerable<ChatCompletionChunk>? stream = await GetStreamFromRouterAsync(request, cancellationToken);
        
        // If stream is null or an error occurred (which would have set the response status),
        // we don't need to proceed
        if (stream == null || Response.StatusCode != StatusCodes.Status200OK)
        {
            return;
        }

        // Process the stream
        await ProcessStreamAsync(stream, request.Model, cancellationToken);
    }

    /// <summary>
    /// Configures the HTTP response for Server-Sent Events.
    /// </summary>
    private void ConfigureResponseForSSE()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // Useful for Nginx environments
    }
    
    /// <summary>
    /// Gets a stream of chat completion chunks from the router.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The stream of chat completion chunks, or null if an error occurred.</returns>
    private async Task<IAsyncEnumerable<ChatCompletionChunk>?> GetStreamFromRouterAsync(
        ChatCompletionRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = _router.StreamChatCompletionAsync(request, cancellationToken: cancellationToken);
            
            // Safety check for null stream
            if (stream == null)
            {
                await HandleErrorAsync(
                    new InvalidOperationException("Router returned null stream"),
                    request.Model,
                    StatusCodes.Status500InternalServerError,
                    "Router failed to provide a stream.",
                    cancellationToken);
                    
                return null;
            }
            
            return stream;
        }
        catch (ModelUnavailableException ex)
        {
            await HandleErrorAsync(
                ex, 
                request.Model, 
                StatusCodes.Status503ServiceUnavailable,
                ex.Message,
                cancellationToken,
                logLevel: LogLevel.Warning);
                
            return null;
        }
        catch (LLMCommunicationException ex)
        {
            await HandleErrorAsync(
                ex, 
                request.Model, 
                StatusCodes.Status502BadGateway,
                $"LLM provider communication error: {ex.Message}",
                cancellationToken);
                
            return null;
        }
        catch (ConfigurationException ex)
        {
            await HandleErrorAsync(
                ex, 
                request.Model, 
                StatusCodes.Status500InternalServerError,
                $"Configuration error: {ex.Message}",
                cancellationToken);
                
            return null;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(
                ex, 
                request.Model, 
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while starting the stream.",
                cancellationToken);
                
            return null;
        }
    }

    /// <summary>
    /// Generic method to handle errors when streaming chat completions.
    /// </summary>
    /// <param name="ex">The exception that was thrown.</param>
    /// <param name="model">The model that was requested.</param>
    /// <param name="statusCode">The HTTP status code to set.</param>
    /// <param name="errorMessage">The error message to send to the client.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="logLevel">The log level to use when logging the error. Defaults to Error.</param>
    /// <returns>A task representing the error handling operation.</returns>
    private async Task HandleErrorAsync(
        Exception ex, 
        string? model, 
        int statusCode, 
        string errorMessage, 
        CancellationToken cancellationToken,
        LogLevel logLevel = LogLevel.Error)
    {
        // Log the error with the appropriate level
        switch (logLevel)
        {
            case LogLevel.Warning:
                _logger.LogWarning(ex, "{ErrorMessage} for model {Model}", errorMessage, model);
                break;
            case LogLevel.Information:
                _logger.LogInformation(ex, "{ErrorMessage} for model {Model}", errorMessage, model);
                break;
            default:
                _logger.LogError(ex, "{ErrorMessage} for model {Model}", errorMessage, model);
                break;
        }
        
        // Only set status code and write error if the response hasn't started yet
        if (!Response.HasStarted)
        {
            Response.StatusCode = statusCode;
            await WriteErrorAsync(errorMessage, cancellationToken);
        }
    }

    /// <summary>
    /// Writes an error message to the response stream in SSE format.
    /// </summary>
    /// <param name="errorMessage">The error message to write.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the write operation.</returns>
    private async Task WriteErrorAsync(string errorMessage, CancellationToken cancellationToken)
    {
        var errorJson = JsonSerializer.Serialize(new { error = errorMessage });
        await Response.WriteAsync($"error: {errorJson}\n\n", cancellationToken);
    }

    /// <summary>
    /// Processes a stream of chat completion chunks and writes them to the response.
    /// </summary>
    /// <param name="stream">The stream of chat completion chunks.</param>
    /// <param name="model">The model that was requested.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the streaming operation.</returns>
    private async Task ProcessStreamAsync(
        IAsyncEnumerable<ChatCompletionChunk> stream, 
        string? model, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Process all chunks in the stream until completion or cancellation
            var streamComplete = await ProcessChunksAsync(stream, model, cancellationToken);
            
            // Send the final [DONE] message only if the stream completed normally without cancellation
            if (streamComplete && !cancellationToken.IsCancellationRequested)
            {
                await WriteSseMessageAsync("[DONE]", model, cancellationToken);
                _logger.LogInformation("Finished streaming successfully for model {Model}", model);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Streaming operation cancelled by client for model {Model}", model);
            // Don't write [DONE] if cancelled
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(
                ex,
                model,
                StatusCodes.Status500InternalServerError,
                "Error processing stream",
                cancellationToken);
            // Don't write [DONE] on error
        }
    }
    
    /// <summary>
    /// Processes all chunks in a stream, writing each to the response.
    /// </summary>
    /// <param name="stream">The stream of chat completion chunks.</param>
    /// <param name="model">The model that was requested.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that resolves to true if all chunks were processed successfully, false otherwise.</returns>
    private async Task<bool> ProcessChunksAsync(
        IAsyncEnumerable<ChatCompletionChunk> stream,
        string? model,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Streaming cancelled by client for model {Model}", model);
                    return false;
                }

                // Serialize and send the chunk as an SSE message
                var jsonString = JsonSerializer.Serialize(chunk);
                if (!await WriteSseMessageAsync(jsonString, model, cancellationToken))
                {
                    return false; // Return false if writing failed
                }
            }
            
            return true; // All chunks processed successfully
        }
        catch (Exception)
        {
            // Let the calling method handle the exception
            throw;
        }
    }

    /// <summary>
    /// Writes a message to the response stream in Server-Sent Events (SSE) format.
    /// </summary>
    /// <param name="message">The message content to write (already serialized).</param>
    /// <param name="model">The model that produced the message (for logging).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that resolves to true if the write was successful, false otherwise.</returns>
    private async Task<bool> WriteSseMessageAsync(
        string message, 
        string? model, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Format the message in SSE format with "data:" prefix and double newline
            var sseMessage = $"data: {message}\n\n";
            
            // Write and flush the message to the response stream
            await Response.Body.WriteAsync(
                Encoding.UTF8.GetBytes(sseMessage), 
                cancellationToken);
                
            await Response.Body.FlushAsync(cancellationToken);
            
            // Log at trace level to avoid excessive logging with large streams
            _logger.LogTrace("Sent SSE message for model {Model}", model);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Streaming cancelled during write for model {Model}", model);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to response stream for model {Model}", model);
            return false;
        }
    }

    /// <summary>
    /// Legacy endpoint for text completions (not implemented).
    /// </summary>
    /// <returns>A 501 Not Implemented response directing users to the chat/completions endpoint.</returns>
    /// <remarks>
    /// This endpoint is included for compatibility with OpenAI API clients that may
    /// attempt to use the legacy completions endpoint, but it is not implemented.
    /// Users should use the chat/completions endpoint instead.
    /// </remarks>
    /// <response code="501">Always returns not implemented with a message to use chat/completions.</response>
    [HttpPost("completions")]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Completions()
    {
        _logger.LogInformation("Legacy /completions endpoint called.");
        // Consider adding a deprecation warning header
        // Response.Headers.Append("Warning", "299 - \"Deprecated endpoint: Please use /chat/completions instead.\"");
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "The /completions endpoint is not implemented. Please use /chat/completions." });
    }

    /// <summary>
    /// Creates vector embeddings for the input text.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An embedding response containing vector representations or an appropriate error.</returns>
    /// <remarks>
    /// This endpoint is compatible with OpenAI's embeddings API and allows creating
    /// vector representations of text that can be used for semantic search, clustering,
    /// or other machine learning tasks.
    /// 
    /// NOTE: As of the current implementation, this functionality may be partially implemented
    /// or under development in the router.
    /// </remarks>
    /// <response code="200">Returns the embedding vectors.</response>
    /// <response code="400">If the request body is invalid or malformed.</response>
    /// <response code="500">If an unexpected error occurs.</response>
    /// <response code="501">If embedding functionality is not yet implemented in the router.</response>
    /// <response code="502">If there's an error communicating with the LLM provider.</response>
    /// <response code="503">If the requested model is unavailable.</response>
    [HttpPost("embeddings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Embeddings(CancellationToken cancellationToken)
    {
        // Parse and validate the request
        EmbeddingRequest? request = await ParseRequestAsync<EmbeddingRequest>(cancellationToken);
        if (request == null)
        {
            return BadRequest(new { error = "Invalid request body." });
        }

        try
        {
            // Currently embeddings are not fully implemented in the router
            _logger.LogWarning("Embeddings endpoint called but CreateEmbeddingAsync not implemented on router.");
            return StatusCode(StatusCodes.Status501NotImplemented, 
                new { error = "Embeddings routing not yet implemented." });
            
            // Once implemented, it will look like this:
            // var response = await _router.CreateEmbeddingAsync(request, cancellationToken: cancellationToken);
            // return Ok(response);
        }
        catch (Exception ex)
        {
            // Reuse our exception handling method for consistency
            return HandleApiException(ex, request.Model, cancellationToken);
        }
    }
    
    /// <summary>
    /// Handles exceptions that can occur in any API endpoint.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="model">The model that was requested.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An appropriate action result for the exception.</returns>
    private IActionResult HandleApiException(
        Exception ex, 
        string? model, 
        CancellationToken cancellationToken)
    {
        // NOTE: This is similar to HandleChatCompletionException but generalizes it for all API endpoints
        if (ex is ModelUnavailableException modelEx)
        {
            _logger.LogWarning(modelEx, "Model unavailable for request: {Model}", model);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = modelEx.Message });
        }
        else if (ex is LLMCommunicationException commEx)
        {
            _logger.LogError(commEx, "Communication error for model {Model}", model);
            return StatusCode(StatusCodes.Status502BadGateway, 
                new { error = $"LLM provider communication error: {commEx.Message}" });
        }
        else if (ex is ConfigurationException configEx)
        {
            _logger.LogError(configEx, "Configuration error for model {Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = $"Configuration error: {configEx.Message}" });
        }
        else if (ex is NotImplementedException)
        {
            _logger.LogWarning("Feature not implemented for model {Model}", model);
            return StatusCode(StatusCodes.Status501NotImplemented, 
                new { error = "This functionality is not yet implemented." });
        }
        else if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Request cancelled by client for model {Model}.", model);
            return StatusCode(StatusCodes.Status400BadRequest, new { error = "Request cancelled." });
        }
        else
        {
            _logger.LogError(ex, "Unexpected error for model {Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Lists all available models that can be used with the API.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of available models and their details.</returns>
    /// <remarks>
    /// This endpoint is compatible with OpenAI's models endpoint and returns a list
    /// of models that are available for use with the API. The response format follows
    /// OpenAI's format with a 'data' array containing model objects.
    /// 
    /// NOTE: The full model details functionality may be partially implemented or
    /// under development in the router.
    /// </remarks>
    /// <response code="200">Returns a list of available models.</response>
    /// <response code="400">If the request is canceled.</response>
    /// <response code="500">If an unexpected error occurs.</response>
    [HttpGet("models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)
    {
        try
        {
            // Currently using the basic model listing approach
            _logger.LogInformation("Getting available models");
            
            // Get model names from the router
            var modelNames = _router.GetAvailableModels();
            
            // Convert to OpenAI format
            var basicModelData = modelNames.Select(m => new { 
                id = m, 
                @object = "model" 
            }).ToList();
            
            // Create the response envelope
            var response = new { 
                data = basicModelData, 
                @object = "list" 
            };
            
            // Maintain the async pattern for future implementation
            await Task.CompletedTask;
            
            return Ok(response);

            // Future implementation with more details:
            // var models = await _router.GetAvailableModelDetailsAsync(cancellationToken);
            // var response = new { data = models, @object = "list" };
            // return Ok(response);
        }
        catch (Exception ex)
        {
            // Use our generalized exception handler with a null model name
            return HandleApiException(ex, null, cancellationToken);
        }
    }

    /// <summary>
    /// Provides a basic health check endpoint for the API.
    /// </summary>
    /// <returns>A simple health status response.</returns>
    /// <remarks>
    /// This endpoint can be used by monitoring tools or load balancers to check
    /// if the API is up and responding. It doesn't require authentication.
    /// </remarks>
    /// <response code="200">Always returns OK with a status and timestamp.</response>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        // Health check endpoint (no auth required)
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}
