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

[ApiController]
[Route("api/v1")]
public class LlmApiController : ControllerBase
{
    private readonly ILogger<LlmApiController> _logger;
    private readonly ILLMRouter _router;

    public LlmApiController(
        ILogger<LlmApiController> logger,
        ILLMRouter router)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions(CancellationToken cancellationToken)
    {
        // Middleware should have validated the virtual key by now.
        // Consider adding an explicit check if HttpContext.Items["ValidatedVirtualKeyId"] exists if needed.

        ChatCompletionRequest? request;
        try
        {
            // Use System.Text.Json binding
            request = await Request.ReadFromJsonAsync<ChatCompletionRequest>(cancellationToken);
            if (request == null)
            {
                return BadRequest(new { error = "Invalid request body." });
            }
        }
        catch (JsonException jsonEx)
        {
            _logger.LogWarning(jsonEx, "Failed to deserialize ChatCompletionRequest");
            return BadRequest(new { error = $"Invalid JSON request: {jsonEx.Message}" });
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error reading request body for chat completions");
             return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to read request body." });
        }

            if (request.Stream == true)
            {
                // Delegate to the streaming method (Phase 2)
                // This method now writes directly to the response stream.
                await StreamChatCompletionsInternal(request, cancellationToken);
                // Since the response is handled within the streaming method, return nothing here.
                // The framework handles completing the response once StreamChatCompletionsInternal finishes.
                // Returning an empty result or specific streaming result might be needed depending on framework nuances.
                // For now, assume the direct writing is sufficient. Consider returning `EmptyResult` if issues arise.
                return new EmptyResult(); // Indicate response is handled elsewhere
            }
            else
            {
            // Handle non-streaming case
            try
            {
                // Pass HttpContext.RequestAborted or the method's cancellationToken
                var response = await _router.CreateChatCompletionAsync(request, cancellationToken: cancellationToken);
                // Note: We might need to pass the API key from the virtual key context if the router/client needs it.
                // Assuming the client factory handles credential lookup based on the routed model for now.
                return Ok(response);
            }
            catch (ModelUnavailableException ex)
            {
                _logger.LogWarning(ex, "Model unavailable for chat completion request: {Model}", request.Model);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
            }
            catch (LLMCommunicationException ex)
            {
                _logger.LogError(ex, "Communication error during chat completion for model {Model}", request.Model);
                // 502 might be more appropriate if it's a downstream provider issue
                return StatusCode(StatusCodes.Status502BadGateway, new { error = $"LLM provider communication error: {ex.Message}" });
            }
            catch (ConfigurationException ex)
            {
                 _logger.LogError(ex, "Configuration error during chat completion for model {Model}", request.Model);
                 return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Configuration error: {ex.Message}" });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                 _logger.LogInformation("Chat completion request cancelled by client.");
                 // Return a specific status code? 499 Client Closed Request is common but non-standard.
                 // Or let the framework handle it. For now, let it propagate or return a generic error.
                 return StatusCode(StatusCodes.Status400BadRequest, new { error = "Request cancelled." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during chat completion for model {Model}", request.Model);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred." });
            }
        }
    }

    // Handles streaming responses using Server-Sent Events (SSE)
    private async Task StreamChatCompletionsInternal(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to stream chat completion for model {Model}", request.Model);
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // Useful for Nginx environments

        IAsyncEnumerable<ChatCompletionChunk>? stream = null;
        try
        {
            // Get the stream from the router
            // *** Requires StreamChatCompletionAsync on ILLMRouter ***
            stream = _router.StreamChatCompletionAsync(request, cancellationToken: cancellationToken);
        }
        catch (ModelUnavailableException ex)
        {
            _logger.LogWarning(ex, "Model unavailable for streaming chat completion request: {Model}", request.Model);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsync($"error: {JsonSerializer.Serialize(new { error = ex.Message })}\n\n", cancellationToken);
            return;
        }
        catch (LLMCommunicationException ex)
        {
            _logger.LogError(ex, "Communication error starting stream for model {Model}", request.Model);
            Response.StatusCode = StatusCodes.Status502BadGateway;
            await Response.WriteAsync($"error: {JsonSerializer.Serialize(new { error = $"LLM provider communication error: {ex.Message}" })}\n\n", cancellationToken);
            return;
        }
        catch (ConfigurationException ex)
        {
             _logger.LogError(ex, "Configuration error starting stream for model {Model}", request.Model);
             Response.StatusCode = StatusCodes.Status500InternalServerError;
             await Response.WriteAsync($"error: {JsonSerializer.Serialize(new { error = $"Configuration error: {ex.Message}" })}\n\n", cancellationToken);
             return;
        }
        catch (Exception ex) // Catch unexpected errors before streaming starts
        {
            _logger.LogError(ex, "Unexpected error starting stream for model {Model}", request.Model);
            // Avoid writing to response if headers already sent, check Response.HasStarted
            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
                await Response.WriteAsync($"error: {JsonSerializer.Serialize(new { error = "An unexpected error occurred starting the stream." })}\n\n", cancellationToken);
            }
            return;
        }

        // If stream is null (shouldn't happen if router impl is correct, but safety check)
        if (stream == null)
        {
             _logger.LogError("Router returned null stream for model {Model}", request.Model);
             if (!Response.HasStarted)
             {
                 Response.StatusCode = StatusCodes.Status500InternalServerError;
                 await Response.WriteAsync($"error: {JsonSerializer.Serialize(new { error = "Router failed to provide a stream." })}\n\n", cancellationToken);
             }
             return;
        }

        // Stream the chunks
        try
        {
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Streaming cancelled by client for model {Model}", request.Model);
                    break;
                }

                try
                {
                    // Use System.Text.Json options for camelCase etc. if needed
                    var jsonString = JsonSerializer.Serialize(chunk);
                    var sseMessage = $"data: {jsonString}\n\n";
                    await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseMessage), cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    _logger.LogTrace("Sent chunk for model {Model}: {ChunkData}", request.Model, jsonString);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Streaming cancelled during write for model {Model}", request.Model);
                    break; // Exit loop if cancellation occurs during write/flush
                }
                catch (Exception ex) // Catch errors during write/flush (e.g., client disconnected)
                {
                    _logger.LogError(ex, "Error writing chunk to response stream for model {Model}", request.Model);
                    // Cannot change status code now, just stop streaming
                    break;
                }
            }

            // Send the final [DONE] message if the loop completed without cancellation/error
            if (!cancellationToken.IsCancellationRequested)
            {
                 await Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data: [DONE]\n\n"), cancellationToken);
                 await Response.Body.FlushAsync(cancellationToken);
                 _logger.LogInformation("Finished streaming successfully for model {Model}", request.Model);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation("Streaming operation cancelled by client for model {Model}", request.Model);
             // Don't write [DONE] if cancelled
        }
        catch (Exception ex) // Catch errors during the await foreach itself (e.g., from the source enumerable)
        {
            _logger.LogError(ex, "Error during stream iteration for model {Model}", request.Model);
            // Don't write [DONE] on error
            // If headers haven't started, maybe try to send an error status? Unlikely here.
        }
    }

    [HttpPost("completions")]
    public IActionResult Completions()
    {
        _logger.LogInformation("Legacy /completions endpoint called.");
        // Consider adding a deprecation warning header
        // Response.Headers.Append("Warning", "299 - \"Deprecated endpoint: Please use /chat/completions instead.\"");
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "The /completions endpoint is not implemented. Please use /chat/completions." });
    }

    [HttpPost("embeddings")]
    public async Task<IActionResult> Embeddings(CancellationToken cancellationToken)
    {
        EmbeddingRequest? request;
        try
        {
            request = await Request.ReadFromJsonAsync<EmbeddingRequest>(cancellationToken);
             if (request == null)
            {
                return BadRequest(new { error = "Invalid request body." });
            }
        }
        catch (JsonException jsonEx)
        {
            _logger.LogWarning(jsonEx, "Failed to deserialize EmbeddingRequest");
            return BadRequest(new { error = $"Invalid JSON request: {jsonEx.Message}" });
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error reading request body for embeddings");
             return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to read request body." });
        }

        try
        {
            // *** Requires CreateEmbeddingAsync on ILLMRouter returning Task<EmbeddingResponse> ***
            // Uncomment the following lines once the router method is implemented:
            // var response = await _router.CreateEmbeddingAsync(request, cancellationToken: cancellationToken);
            // return Ok(response);

            // Placeholder until router method exists:
             _logger.LogWarning("Embeddings endpoint called but CreateEmbeddingAsync not implemented on router.");
            return StatusCode(StatusCodes.Status501NotImplemented, new { error = "Embeddings routing not yet implemented." });
        }
        catch (ModelUnavailableException ex)
        {
            _logger.LogWarning(ex, "Model unavailable for embeddings request: {Model}", request.Model);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (LLMCommunicationException ex)
        {
            _logger.LogError(ex, "Communication error during embeddings request for model {Model}", request.Model);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"LLM provider communication error: {ex.Message}" });
        }
        catch (ConfigurationException ex)
        {
             _logger.LogError(ex, "Configuration error during embeddings request for model {Model}", request.Model);
             return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Configuration error: {ex.Message}" });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation("Embeddings request cancelled by client.");
             return StatusCode(StatusCodes.Status400BadRequest, new { error = "Request cancelled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during embeddings request for model {Model}", request.Model);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred." });
        }
    }

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)
    {
        try
        {
            // *** Requires GetAvailableModelDetailsAsync on ILLMRouter returning Task<IReadOnlyList<ModelInfo>> ***
            // Uncomment the following lines once the router method is implemented:
            // var models = await _router.GetAvailableModelDetailsAsync(cancellationToken);
            // var response = new { data = models, @object = "list" }; // Assumes ModelInfo has necessary properties for OpenAI format
            // return Ok(response);

            // Placeholder/Fallback using existing basic router method:
            _logger.LogWarning("Models endpoint called but GetAvailableModelDetailsAsync not implemented on router. Using basic GetAvailableModels().");
            var modelNames = _router.GetAvailableModels(); // Use existing basic method for now
            var basicModelData = modelNames.Select(m => new { id = m, @object = "model" }).ToList(); // Basic structure
            var response = new { data = basicModelData, @object = "list" };
            await Task.CompletedTask; // To make the method async as per original signature if needed, or adjust signature
            return Ok(response);

        }
        catch (ConfigurationException ex)
        {
             _logger.LogError(ex, "Configuration error retrieving available models");
             return StatusCode(StatusCodes.Status500InternalServerError, new { error = $"Configuration error: {ex.Message}" });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
             _logger.LogInformation("Get models request cancelled by client.");
             return StatusCode(StatusCodes.Status400BadRequest, new { error = "Request cancelled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving available models");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred retrieving models." });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        // Health check endpoint (no auth required)
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}
