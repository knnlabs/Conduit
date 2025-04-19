using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service to manage context window sizes in LLM requests by trimming messages to fit within token limits.
    /// </summary>
    public class ContextManager : IContextManager
    {
        private readonly ITokenCounter _tokenCounter;
        private readonly ILogger<ContextManager> _logger;

        public ContextManager(ITokenCounter tokenCounter, ILogger<ContextManager> logger)
        {
            _tokenCounter = tokenCounter;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ChatCompletionRequest> ManageContextAsync(ChatCompletionRequest request, int? maxContextTokens)
        {
            if (maxContextTokens == null || maxContextTokens <= 0 || request.Messages == null || !request.Messages.Any())
            {
                return request; // Nothing to do if no limit or no messages
            }

            int currentTokens = await _tokenCounter.EstimateTokenCountAsync(request.Model, request.Messages);

            // Account for completion tokens if MaxTokens is specified in the request
            int reservedCompletionTokens = request.MaxTokens ?? 0;
            
            // Calculate available tokens for the context window
            int availableContextTokens = maxContextTokens.Value - reservedCompletionTokens;
            
            // Ensure a minimum context size (don't trim to nothing)
            if (availableContextTokens < 100)
            {
                _logger.LogWarning("Available context tokens ({AvailableTokens}) is very low due to high MaxTokens ({MaxTokens}) setting. Using minimum context size.", 
                    availableContextTokens, request.MaxTokens);
                availableContextTokens = 100; // Minimum reasonable context window
            }

            if (currentTokens <= availableContextTokens)
            {
                return request; // Already within limits
            }

            _logger.LogInformation(
                "Context trimming activated. Current tokens: {CurrentTokens}, Max available context tokens: {MaxTokens}",
                currentTokens, availableContextTokens);

            // Create a copy of the messages for trimming
            var trimmedMessages = new List<Message>(request.Messages);
            
            // Track system messages for special handling
            var systemMessages = trimmedMessages
                .Where(m => m.Role == MessageRole.System)
                .ToList();
            
            // Keep trimming until under the limit or only system messages remain
            while (currentTokens > availableContextTokens && 
                   trimmedMessages.Count > systemMessages.Count)
            {
                // Find the oldest non-system message (typically user or assistant message)
                int indexToRemove = -1;
                for (int i = 0; i < trimmedMessages.Count; i++)
                {
                    if (trimmedMessages[i].Role != MessageRole.System)
                    {
                        indexToRemove = i;
                        break;
                    }
                }

                if (indexToRemove == -1)
                {
                    // Only system messages left, but still over limit
                    _logger.LogWarning(
                        "Context trimming couldn't reduce token count below limit. " +
                        "Only system messages remain but token count {CurrentTokens} exceeds limit {MaxTokens}.",
                        currentTokens, availableContextTokens);
                    
                    // We could potentially trim system messages too, but for now let's preserve them
                    break;
                }

                // Log which message is being removed
                var messageToRemove = trimmedMessages[indexToRemove];
                _logger.LogDebug("Trimming message with role {Role}, content starts with: {ContentPreview}",
                    messageToRemove.Role, 
                    GetContentPreview(messageToRemove.Content, 20));
                
                // Remove the message
                trimmedMessages.RemoveAt(indexToRemove);
                
                // Re-estimate token count after removal
                currentTokens = await _tokenCounter.EstimateTokenCountAsync(request.Model, trimmedMessages);
            }

            // If we removed messages, create a new request with trimmed messages
            if (trimmedMessages.Count < request.Messages.Count)
            {
                _logger.LogInformation(
                    "Trimmed {RemovedCount} messages to fit context window. Original: {OriginalCount}, New: {NewCount}, Tokens: {TokenCount}",
                    request.Messages.Count - trimmedMessages.Count,
                    request.Messages.Count,
                    trimmedMessages.Count,
                    currentTokens);
                
                // Create a new request with the trimmed messages
                // Clone the request to avoid modifying the original
                var newRequest = new ChatCompletionRequest
                {
                    Model = request.Model,
                    Messages = trimmedMessages,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    TopP = request.TopP,
                    N = request.N,
                    Stream = request.Stream,
                    Stop = request.Stop,
                    User = request.User
                };
                
                return newRequest;
            }
            
            // If no trimming was done, return the original request
            return request;
        }

        /// <summary>
        /// Gets a preview of message content, handling both string and multimodal content.
        /// </summary>
        private string GetContentPreview(object? content, int maxLength)
        {
            if (content == null)
                return "[null]";
                
            if (content is string textContent)
            {
                // Simple string content
                return textContent.Length <= maxLength 
                    ? textContent 
                    : textContent.Substring(0, maxLength) + "...";
            }
            
            if (content is JsonElement jsonElement)
            {
                // Handle JsonElement (common when deserialized from JSON)
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    string? stringValue = jsonElement.GetString();
                    if (!string.IsNullOrEmpty(stringValue) && stringValue.Length > maxLength)
                        return stringValue.Substring(0, maxLength) + "...";
                    return stringValue ?? "[empty]";
                }
                else if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    return "[Multimodal content with " + jsonElement.GetArrayLength() + " parts]";
                }
                else
                {
                    return jsonElement.ToString() ?? "[JsonElement]";
                }
            }
            
            try
            {
                // Try to provide a sensible preview for other object types
                string json = JsonSerializer.Serialize(content);
                if (json.Length > maxLength)
                    return json.Substring(0, maxLength) + "...";
                return json;
            }
            catch
            {
                // Fallback
                return content.ToString() ?? "[object]";
            }
        }
    }
}
