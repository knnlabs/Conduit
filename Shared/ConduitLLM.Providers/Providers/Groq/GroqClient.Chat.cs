using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.Logging;

using CoreModels = ConduitLLM.Core.Models;

namespace ConduitLLM.Providers.Groq
{
    /// <summary>
    /// GroqClient partial class containing chat completion methods.
    /// </summary>
    public partial class GroqClient
    {
        /// <summary>
        /// Creates a chat completion with enhanced error handling specific to Groq.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A chat completion response from Groq.</returns>
        /// <exception cref="LLMCommunicationException">Thrown when there is a communication error with Groq.</exception>
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.CreateChatCompletionAsync(request, apiKey, cancellationToken);
            }
            catch (LLMCommunicationException ex)
            {
                // Enhance error message handling for Groq and re-throw
                var enhancedErrorMessage = ExtractEnhancedErrorMessage(ex);
                throw new LLMCommunicationException(enhancedErrorMessage, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Handle other exceptions not caught by the base class
                var errorMessage = ex.Message;
                if (ex is HttpRequestException httpEx && httpEx.Data["Body"] is string body)
                {
                    errorMessage = body;
                }

                Logger.LogError(ex, "Groq API error: {Message}", errorMessage);
                throw new LLMCommunicationException($"Groq API error: {errorMessage}", ex);
            }
        }

        /// <summary>
        /// Transforms raw chunk JSON to extract Groq's x_groq.usage into the standard usage field.
        /// </summary>
        protected override string TransformChunkJson(System.Text.Json.JsonElement chunk)
            => ProcessGroqChunkJson(chunk);

        /// <summary>
        /// Processes a Groq chunk JSON to extract x_groq.usage and map it to the standard usage field.
        /// </summary>
        private string ProcessGroqChunkJson(System.Text.Json.JsonElement chunk)
        {
            try
            {
                // Check if x_groq.usage exists
                if (chunk.TryGetProperty("x_groq", out var xGroq) && 
                    xGroq.TryGetProperty("usage", out var xGroqUsage))
                {
                    // Create a mutable copy of the chunk
                    using var doc = System.Text.Json.JsonDocument.Parse(chunk.GetRawText());
                    using var stream = new System.IO.MemoryStream();
                    using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
                    {
                        writer.WriteStartObject();
                        
                        // Copy all existing properties
                        foreach (var property in doc.RootElement.EnumerateObject())
                        {
                            // Skip x_groq as we're extracting its usage data
                            if (property.Name != "x_groq")
                            {
                                property.WriteTo(writer);
                            }
                        }
                        
                        // Add usage field with data from x_groq.usage
                        writer.WritePropertyName("usage");
                        writer.WriteStartObject();
                        
                        if (xGroqUsage.TryGetProperty("prompt_tokens", out var promptTokens))
                        {
                            writer.WriteNumber("prompt_tokens", promptTokens.GetInt32());
                        }
                        
                        if (xGroqUsage.TryGetProperty("completion_tokens", out var completionTokens))
                        {
                            writer.WriteNumber("completion_tokens", completionTokens.GetInt32());
                        }
                        
                        if (xGroqUsage.TryGetProperty("total_tokens", out var totalTokens))
                        {
                            writer.WriteNumber("total_tokens", totalTokens.GetInt32());
                        }
                        
                        writer.WriteEndObject(); // End usage
                        writer.WriteEndObject(); // End root
                    }
                    
                    var processedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                    
                    Logger.LogDebug(
                        "Extracted Groq usage data: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                        xGroqUsage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                        xGroqUsage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0,
                        xGroqUsage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0);
                    
                    return processedJson;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to process Groq chunk JSON for usage extraction");
            }
            
            // Return original JSON if no x_groq.usage found or processing failed
            return chunk.GetRawText();
        }

        /// <summary>
        /// Maps the Groq non-streaming response to provider-agnostic format, handling Groq's usage data.
        /// </summary>
        /// <param name="responseObj">The response from the Groq API.</param>
        /// <param name="originalModelAlias">The original model alias from the request.</param>
        /// <returns>A provider-agnostic chat completion response with usage data.</returns>
        /// <remarks>
        /// Groq returns usage data in both standard usage field and x_groq field for non-streaming responses.
        /// This method ensures we capture the usage data correctly.
        /// </remarks>
        protected override CoreModels.ChatCompletionResponse MapFromOpenAIResponse(
            object responseObj,
            string? originalModelAlias)
        {
            // Get the base mapping first
            var mappedResponse = base.MapFromOpenAIResponse(responseObj, originalModelAlias);
            
            // For non-streaming, Groq typically includes usage in the standard field
            // But let's verify it was captured correctly
            if (mappedResponse.Usage != null)
            {
                Logger.LogDebug(
                    "Groq non-streaming usage data captured: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                    mappedResponse.Usage.PromptTokens,
                    mappedResponse.Usage.CompletionTokens,
                    mappedResponse.Usage.TotalTokens);
            }
            else
            {
                Logger.LogWarning("No usage data found in Groq non-streaming response");
            }
            
            return mappedResponse;
        }
    }
}
