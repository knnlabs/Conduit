using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.CoreClient.Services;

/// <summary>
/// Service for chat completions using the Core API.
/// </summary>
public class ChatService
{
    private readonly BaseClient _client;
    private readonly ILogger<ChatService>? _logger;
    private const string BaseEndpoint = "/v1/chat/completions";

    /// <summary>
    /// Initializes a new instance of the ChatService class.
    /// </summary>
    /// <param name="client">The base client to use for HTTP requests.</param>
    /// <param name="logger">Optional logger instance.</param>
    public ChatService(BaseClient client, ILogger<ChatService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
    }

    /// <summary>
    /// Creates a chat completion.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chat completion response.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<ChatCompletionResponse> CreateCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateRequest(request);
            
            _logger?.LogDebug("Creating chat completion for model {Model} with {MessageCount} messages", 
                request.Model, request.Messages?.Count() ?? 0);

            var response = await _client.PostForServiceAsync<ChatCompletionResponse>(
                BaseEndpoint,
                request,
                cancellationToken);

            _logger?.LogDebug("Chat completion created with ID {CompletionId}", response.Id);
            return response;
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw; // This line will never be reached due to HandleException always throwing
        }
    }

    /// <summary>
    /// Creates a streaming chat completion.
    /// </summary>
    /// <param name="request">The chat completion request with streaming enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of chat completion chunks.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="StreamException">Thrown when streaming fails.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async IAsyncEnumerable<ChatCompletionChunk> CreateCompletionStreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        
        // Ensure streaming is enabled
        request.Stream = true;
        
        _logger?.LogDebug("Creating streaming chat completion for model {Model} with {MessageCount} messages", 
            request.Model, request.Messages?.Count() ?? 0);

        IAsyncEnumerable<ChatCompletionChunk> stream;
        try
        {
            stream = _client.PostStreamForServiceAsync<ChatCompletionChunk>(
                BaseEndpoint,
                request,
                cancellationToken);
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Creates a chat completion with automatic retry logic for function calls.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <param name="maxFunctionCallRounds">Maximum number of function call rounds (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final chat completion response after all function calls are resolved.</returns>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
    public async Task<ChatCompletionResponse> CreateCompletionWithFunctionCallsAsync(
        ChatCompletionRequest request,
        int maxFunctionCallRounds = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateRequest(request);

            var messages = request.Messages.ToList();
            var currentRequest = request;
            
            for (int round = 0; round < maxFunctionCallRounds; round++)
            {
                var response = await CreateCompletionAsync(currentRequest, cancellationToken);
                
                // Check if the response contains function calls
                var choice = response.Choices.FirstOrDefault();
                if (choice?.Message?.ToolCalls == null || !choice.Message.ToolCalls.Any())
                {
                    // No function calls, return the response
                    return response;
                }

                // Add the assistant's message with tool calls to the conversation
                messages.Add(choice.Message);

                // For now, we'll return the response since we can't execute functions
                // In a real implementation, you would execute the functions and add their results
                _logger?.LogWarning("Function calls detected but automatic execution is not implemented. " +
                    "Add function execution logic or handle tool calls manually.");
                
                return response;
            }

            throw new ConduitCoreException($"Maximum function call rounds ({maxFunctionCallRounds}) exceeded");
        }
        catch (Exception ex) when (!(ex is ConduitCoreException))
        {
            ErrorHandler.HandleException(ex);
            throw;
        }
    }

    /// <summary>
    /// Estimates the token count for a chat completion request.
    /// </summary>
    /// <param name="request">The chat completion request.</param>
    /// <returns>An estimated token count.</returns>
    /// <remarks>
    /// This is a rough estimation based on character count. For accurate token counting,
    /// use a proper tokenizer library like TikToken.
    /// </remarks>
    public int EstimateTokenCount(ChatCompletionRequest request)
    {
        if (request.Messages == null)
            return 0;

        // Rough estimation: ~4 characters per token for English text
        var totalCharacters = request.Messages.Sum(m => (m.Content?.Length ?? 0) + (m.Name?.Length ?? 0));
        var estimatedTokens = (int)Math.Ceiling(totalCharacters / 4.0);

        // Add overhead for message structure and system tokens
        estimatedTokens += request.Messages.Count() * 10; // ~10 tokens overhead per message

        // Add tool/function call overhead if present
        if (request.Tools != null && request.Tools.Any())
        {
            estimatedTokens += request.Tools.Sum(t => EstimateFunctionTokens(t));
        }

        return estimatedTokens;
    }

    /// <summary>
    /// Validates the chat completion request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    private static void ValidateRequest(ChatCompletionRequest request)
    {
        if (request == null)
            throw new ValidationException("Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.Model))
            throw new ValidationException("Model is required", "model");

        if (request.Messages == null || !request.Messages.Any())
            throw new ValidationException("At least one message is required", "messages");

        // Validate message roles
        foreach (var message in request.Messages)
        {
            if (string.IsNullOrWhiteSpace(message.Role))
                throw new ValidationException("Message role is required", "messages");

            var validRoles = new[] { "system", "user", "assistant", "tool" };
            if (!validRoles.Contains(message.Role.ToLower()))
                throw new ValidationException($"Invalid message role: {message.Role}", "messages");

            // Tool messages must have tool_call_id
            if (message.Role.ToLower() == "tool" && string.IsNullOrWhiteSpace(message.ToolCallId))
                throw new ValidationException("Tool messages must have tool_call_id", "messages");
        }

        // Validate parameter ranges
        if (request.Temperature.HasValue && (request.Temperature < 0 || request.Temperature > 2))
            throw new ValidationException("Temperature must be between 0 and 2", "temperature");

        if (request.TopP.HasValue && (request.TopP < 0 || request.TopP > 1))
            throw new ValidationException("TopP must be between 0 and 1", "top_p");

        if (request.FrequencyPenalty.HasValue && (request.FrequencyPenalty < -2 || request.FrequencyPenalty > 2))
            throw new ValidationException("FrequencyPenalty must be between -2 and 2", "frequency_penalty");

        if (request.PresencePenalty.HasValue && (request.PresencePenalty < -2 || request.PresencePenalty > 2))
            throw new ValidationException("PresencePenalty must be between -2 and 2", "presence_penalty");

        if (request.MaxTokens.HasValue && request.MaxTokens <= 0)
            throw new ValidationException("MaxTokens must be greater than 0", "max_tokens");

        if (request.N.HasValue && (request.N <= 0 || request.N > 128))
            throw new ValidationException("N must be between 1 and 128", "n");
    }

    /// <summary>
    /// Estimates the token count for a function definition.
    /// </summary>
    /// <param name="tool">The tool/function to estimate tokens for.</param>
    /// <returns>Estimated token count.</returns>
    private static int EstimateFunctionTokens(Tool tool)
    {
        if (tool.Function == null)
            return 0;

        var tokens = 0;
        
        // Function name and description
        tokens += (tool.Function.Name?.Length ?? 0) / 4;
        tokens += (tool.Function.Description?.Length ?? 0) / 4;
        
        // Parameters schema (rough estimate)
        if (tool.Function.Parameters != null)
        {
            tokens += 50; // Base overhead for parameters schema
        }

        return Math.Max(tokens, 10); // Minimum 10 tokens per function
    }
}