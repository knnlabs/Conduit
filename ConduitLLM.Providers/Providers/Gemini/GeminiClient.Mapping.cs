using System;
using System.Collections.Generic;
using System.Linq;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.Gemini.Models;
using ConduitLLM.Providers.Utilities;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Providers.Gemini
{
    /// <summary>
    /// GeminiClient partial class containing request/response mapping functionality.
    /// </summary>
    public partial class GeminiClient
    {
        /// <summary>
        /// Maps a core chat completion request to Gemini format.
        /// </summary>
        private GeminiGenerateContentRequest MapToGeminiRequest(ChatCompletionRequest coreRequest)
        {
            var contents = new List<GeminiContent>();

            // Extract system message if present
            var systemMessage = coreRequest.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            if (systemMessage != null)
            {
                // Create a special system message
                contents.Add(new GeminiContent
                {
                    Role = "user", // Gemini doesn't have a system role, use user
                    Parts = MapToGeminiParts(systemMessage.Content)
                });
            }

            // Process user/assistant messages
            foreach (var message in coreRequest.Messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                string role = message.Role.ToLowerInvariant() switch
                {
                    "user" => "user",
                    "assistant" => "model",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(role))
                {
                    Logger.LogWarning("Unsupported message role '{Role}' encountered for Gemini provider. Skipping message.", message.Role);
                    continue;
                }

                contents.Add(new GeminiContent
                {
                    Role = role,
                    Parts = MapToGeminiParts(message.Content)
                });
            }

            return new GeminiGenerateContentRequest
            {
                Contents = contents,
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = ParameterConverter.ToTemperature(coreRequest.Temperature),
                    TopP = ParameterConverter.ToProbability(coreRequest.TopP, 0.0, 1.0),
                    TopK = coreRequest.TopK,
                    CandidateCount = coreRequest.N, // Map N to candidateCount
                    MaxOutputTokens = coreRequest.MaxTokens,
                    StopSequences = coreRequest.Stop
                }
            };
        }

        /// <summary>
        /// Maps content from the core model format to Gemini's part format, handling both text and images.
        /// </summary>
        /// <param name="content">The content to map, can be string, list of content parts, or JSON</param>
        /// <returns>A list of Gemini content parts</returns>
        private List<GeminiPart> MapToGeminiParts(object? content)
        {
            var parts = new List<GeminiPart>();

            // Handle simple text content
            if (content is string textContent)
            {
                parts.Add(new GeminiPart { Text = textContent });
                return parts;
            }

            // Check if we have multimodal content
            if (ContentHelper.IsTextOnly(content))
            {
                // For text-only content, just extract as string
                string text = ContentHelper.GetContentAsString(content);
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(new GeminiPart { Text = text });
                }
                return parts;
            }

            // Handle multimodal content (text + images)
            var textParts = ContentHelper.ExtractMultimodalContent(content);
            var imageUrls = ContentHelper.ExtractImageUrls(content);

            // Add text parts
            if (textParts.Any())
            {
                // Join all text parts with newlines to preserve formatting
                string combinedText = string.Join(Environment.NewLine, textParts);
                parts.Add(new GeminiPart { Text = combinedText });
            }

            // Add image parts
            foreach (var imageUrl in imageUrls)
            {
                // For Gemini, we need to handle image data encoding
                try
                {
                    if (imageUrl.IsBase64DataUrl)
                    {
                        // Already have base64 data in the URL, extract and use it
                        string mimeType = imageUrl.MimeType ?? "image/jpeg";
                        string base64Data = imageUrl.Base64Data ?? string.Empty;

                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            parts.Add(new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = mimeType,
                                    Data = base64Data
                                }
                            });
                        }
                    }
                    else
                    {
                        // For regular URLs, we need to download the image and convert to base64
                        var imageData = ImageUtility.DownloadImageAsync(imageUrl.Url).GetAwaiter().GetResult();
                        if (imageData != null && imageData.Length > 0)
                        {
                            // Detect mime type from image data
                            string? mimeType = ImageUtility.DetectMimeType(imageData) ?? "image/jpeg";
                            string base64Data = Convert.ToBase64String(imageData);

                            parts.Add(new GeminiPart
                            {
                                InlineData = new GeminiInlineData
                                {
                                    MimeType = mimeType,
                                    Data = base64Data
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to process image URL '{Url}' for Gemini request", imageUrl.Url);
                    // Continue with other content if an image fails
                }
            }

            return parts;
        }

        /// <summary>
        /// Maps a Gemini response to the core chat completion response format.
        /// </summary>
        private ChatCompletionResponse MapToCoreResponse(GeminiGenerateContentResponse geminiResponse, string originalModelAlias)
        {
            if (geminiResponse.Candidates == null || geminiResponse.Candidates.Count == 0)
            {
                throw new LLMCommunicationException("Gemini response contains no candidates");
            }

            var firstCandidate = geminiResponse.Candidates[0];

            if (firstCandidate.Content == null || firstCandidate.Content.Parts == null || !firstCandidate.Content.Parts.Any())
            {
                throw new LLMCommunicationException("Gemini response contains a candidate with no content or parts");
            }

            var firstPart = firstCandidate.Content.Parts.First();

            if (geminiResponse.UsageMetadata == null)
            {
                Logger.LogWarning("Gemini response missing usage metadata. Using default values.");
            }

            var usageMetadata = geminiResponse.UsageMetadata ?? new GeminiUsageMetadata
            {
                PromptTokenCount = 0,
                CandidatesTokenCount = 0,
                TotalTokenCount = 0
            };

            var choice = new Choice
            {
                Index = firstCandidate.Index,
                Message = new Message
                {
                    // Gemini uses "model" for assistant role
                    Role = firstCandidate.Content.Role == "model" ? "assistant" : firstCandidate.Content.Role,
                    Content = firstPart.Text ?? string.Empty // Add null check with empty string default
                },
                FinishReason = MapFinishReason(firstCandidate.FinishReason) ?? "stop" // Default to "stop" if null
            };

            return new ChatCompletionResponse
            {
                Id = Guid.NewGuid().ToString(), // Gemini doesn't provide an ID
                Object = "chat.completion", // Mimic OpenAI structure
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time as Gemini doesn't provide it
                Model = originalModelAlias, // Return the alias the user requested
                Choices = new List<Choice> { choice },
                Usage = new Usage
                {
                    PromptTokens = usageMetadata.PromptTokenCount,
                    CompletionTokens = usageMetadata.CandidatesTokenCount, // Sum across candidates (usually 1)
                    TotalTokens = usageMetadata.TotalTokenCount
                },
                OriginalModelAlias = originalModelAlias
            };
        }

        /// <summary>
        /// Maps a Gemini streaming response to a core chunk.
        /// </summary>
        private ChatCompletionChunk? MapToCoreChunk(GeminiGenerateContentResponse geminiResponse, string originalModelAlias)
        {
            // Extract the relevant delta information from the Gemini response structure
            var firstCandidate = geminiResponse.Candidates?.FirstOrDefault();
            var firstPart = firstCandidate?.Content?.Parts?.FirstOrDefault();
            string? deltaText = firstPart?.Text;
            string? finishReason = MapFinishReason(firstCandidate?.FinishReason);

            // Only yield a chunk if there's actual text content or a finish reason
            if (string.IsNullOrEmpty(deltaText) && string.IsNullOrEmpty(finishReason))
            {
                Logger.LogTrace("Skipping Gemini stream chunk mapping as no delta text or finish reason found.");
                return null;
            }

            var choice = new StreamingChoice
            {
                Index = firstCandidate?.Index ?? 0,
                Delta = new DeltaContent
                {
                    // Gemini doesn't explicitly provide role in delta chunks, assume assistant?
                    Content = deltaText // Can be null if only finish reason is present
                },
                FinishReason = finishReason // Can be null
            };

            return new ChatCompletionChunk
            {
                Id = Guid.NewGuid().ToString(), // Gemini doesn't provide chunk IDs
                Object = "chat.completion.chunk",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), // Use current time
                Model = originalModelAlias,
                Choices = new List<StreamingChoice> { choice },
                OriginalModelAlias = originalModelAlias
                // Usage data is typically aggregated at the end for Gemini, not per chunk
            };
        }

        /// <summary>
        /// Maps Gemini finish reasons to standard OpenAI-compatible finish reasons.
        /// </summary>
        private static string? MapFinishReason(string? geminiFinishReason)
        {
            return geminiFinishReason switch
            {
                "STOP" => "stop", // Normal completion
                "MAX_TOKENS" => "length",
                "SAFETY" => "content_filter", // Map safety stop to content_filter
                "RECITATION" => "content_filter", // Map recitation stop to content_filter
                "OTHER" => null, // Unknown reason
                _ => geminiFinishReason // Pass through null or unknown values
            };
        }
    }
}