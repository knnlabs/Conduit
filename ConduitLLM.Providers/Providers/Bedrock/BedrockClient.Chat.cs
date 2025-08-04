using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Utilities;
using ConduitLLM.Providers.Helpers;
using ConduitLLM.Providers.Providers.Bedrock.Models;

namespace ConduitLLM.Providers.Providers.Bedrock
{
    /// <summary>
    /// BedrockClient partial class containing chat completion methods.
    /// </summary>
    public partial class BedrockClient
    {
        /// <inheritdoc />
        public override async Task<ChatCompletionResponse> CreateChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            ValidateRequest(request, "ChatCompletion");

            return await ExecuteApiRequestAsync(async () =>
            {
                // Determine which model provider is being used
                string modelId = request.Model ?? ProviderModelId;

                if (modelId.Contains("anthropic.claude", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateAnthropicClaudeChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("meta.llama", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateMetaLlamaChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("amazon.titan", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateAmazonTitanChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("cohere.command", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateCohereChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("ai21", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateAI21ChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else if (modelId.Contains("mistral", StringComparison.OrdinalIgnoreCase))
                {
                    return await CreateMistralChatCompletionAsync(request, apiKey, cancellationToken);
                }
                else
                {
                    throw new UnsupportedProviderException($"Unsupported Bedrock model: {modelId}");
                }
            }, "ChatCompletion", cancellationToken);
        }

        private async Task<ChatCompletionResponse> CreateAnthropicClaudeChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Claude format
            var claudeRequest = new BedrockClaudeChatRequest
            {
                MaxTokens = request.MaxTokens,
                Temperature = (float?)request.Temperature,
                TopP = request.TopP.HasValue ? (float?)request.TopP.Value : null,
                Messages = new List<BedrockClaudeMessage>()
            };

            // Extract system message if present
            var systemMessage = request.Messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            if (systemMessage != null)
            {
                // Handle system message content, which could be string or content parts
                claudeRequest.System = ContentHelper.GetContentAsString(systemMessage.Content);
            }

            // Map user and assistant messages
            foreach (var message in request.Messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                claudeRequest.Messages.Add(new BedrockClaudeMessage
                {
                    Role = message.Role.ToLowerInvariant() switch
                    {
                        "user" => "user",
                        "assistant" => "assistant",
                        _ => message.Role // Keep as-is for other roles
                    },
                    Content = new List<BedrockClaudeContent>
                    {
                        new BedrockClaudeContent { Type = "text", Text = ContentHelper.GetContentAsString(message.Content) }
                    }
                });
            }

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            string apiUrl = $"model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, claudeRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockClaudeChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null || bedrockResponse.Content == null || !bedrockResponse.Content.Any())
            {
                throw new LLMCommunicationException("Failed to deserialize the response from AWS Bedrock API or response content is empty");
            }

            // Map to core response format
            return new ChatCompletionResponse
            {
                Id = bedrockResponse.Id ?? Guid.NewGuid().ToString(),
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? ProviderModelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = bedrockResponse.Role ?? "assistant",
                            Content = bedrockResponse.Content.FirstOrDefault()?.Text ?? string.Empty
                        },
                        FinishReason = MapBedrockStopReason(bedrockResponse.StopReason)
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.Usage?.InputTokens ?? 0,
                    CompletionTokens = bedrockResponse.Usage?.OutputTokens ?? 0,
                    TotalTokens = (bedrockResponse.Usage?.InputTokens ?? 0) + (bedrockResponse.Usage?.OutputTokens ?? 0)
                }
            };
        }

        private async Task<ChatCompletionResponse> CreateMetaLlamaChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Llama format
            var llamaRequest = new BedrockLlamaChatRequest
            {
                Prompt = BuildLlamaPrompt(request.Messages),
                MaxGenLen = request.MaxTokens ?? 512,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            string apiUrl = $"model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, llamaRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockLlamaChatResponse>(responseContent, JsonOptions);

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = bedrockResponse?.Generation ?? string.Empty
                        },
                        FinishReason = MapLlamaStopReason(bedrockResponse?.StopReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse?.PromptTokenCount ?? 0,
                    CompletionTokens = bedrockResponse?.GenerationTokenCount ?? 0,
                    TotalTokens = (bedrockResponse?.PromptTokenCount ?? 0) + 
                                  (bedrockResponse?.GenerationTokenCount ?? 0)
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateAmazonTitanChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Titan format
            var titanRequest = new BedrockTitanChatRequest
            {
                InputText = BuildPrompt(request.Messages),
                TextGenerationConfig = new BedrockTitanTextGenerationConfig
                {
                    MaxTokenCount = request.MaxTokens ?? 512,
                    Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                    TopP = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f,
                    StopSequences = request.Stop?.ToList()
                }
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, titanRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockTitanChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null)
            {
                throw new LLMCommunicationException("Failed to deserialize Bedrock Titan response");
            }

            // Get the first result
            var result = bedrockResponse.Results?.FirstOrDefault();
            var responseText = result?.OutputText ?? string.Empty;
            var completionReason = result?.CompletionReason ?? "COMPLETE";

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = responseText
                        },
                        FinishReason = MapTitanCompletionReason(completionReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.InputTextTokenCount ?? 0,
                    CompletionTokens = result?.TokenCount ?? 0,
                    TotalTokens = (bedrockResponse.InputTextTokenCount ?? 0) + (result?.TokenCount ?? 0)
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateCohereChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Cohere format
            var cohereRequest = new BedrockCohereChatRequest
            {
                Prompt = BuildCoherePrompt(request.Messages),
                MaxTokens = request.MaxTokens ?? 1024,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                P = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f,
                K = 0, // Default top-k
                StopSequences = request.Stop?.ToList(),
                Stream = false
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, cohereRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockCohereChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null)
            {
                throw new LLMCommunicationException("Failed to deserialize Bedrock Cohere response");
            }

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = bedrockResponse.GenerationId ?? $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = bedrockResponse.Text ?? string.Empty
                        },
                        FinishReason = MapCohereStopReason(bedrockResponse.FinishReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = bedrockResponse.Meta?.BilledUnits?.InputTokens ?? 0,
                    CompletionTokens = bedrockResponse.Meta?.BilledUnits?.OutputTokens ?? 0,
                    TotalTokens = (bedrockResponse.Meta?.BilledUnits?.InputTokens ?? 0) + 
                                  (bedrockResponse.Meta?.BilledUnits?.OutputTokens ?? 0)
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateAI21ChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock AI21 format
            var ai21Request = new BedrockAI21ChatRequest
            {
                Prompt = BuildPrompt(request.Messages),
                MaxTokens = request.MaxTokens ?? 1024,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : 1.0f,
                StopSequences = request.Stop?.ToList()
            };

            // Add penalties if specified
            if (request.FrequencyPenalty.HasValue)
            {
                ai21Request.CountPenalty = new BedrockAI21Penalty { Scale = (float)request.FrequencyPenalty.Value };
            }
            if (request.PresencePenalty.HasValue)
            {
                ai21Request.PresencePenalty = new BedrockAI21Penalty { Scale = (float)request.PresencePenalty.Value };
            }

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            
            string apiUrl = $"/model/{modelId}/invoke";
            
            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, ai21Request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockAI21ChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse == null)
            {
                throw new LLMCommunicationException("Failed to deserialize Bedrock AI21 response");
            }

            // Get the first completion
            var completion = bedrockResponse.Completions?.FirstOrDefault();
            var responseText = completion?.Data?.Text ?? string.Empty;
            var finishReason = completion?.FinishReason?.Reason ?? "stop";

            // AI21 doesn't provide token counts in the response, so estimate them
            var promptTokens = EstimateTokenCount(BuildPrompt(request.Messages));
            var completionTokens = EstimateTokenCount(responseText);

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = bedrockResponse.Id ?? $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = responseText
                        },
                        FinishReason = MapAI21FinishReason(finishReason),
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                },
                SystemFingerprint = null
            };
        }

        private async Task<ChatCompletionResponse> CreateMistralChatCompletionAsync(
            ChatCompletionRequest request,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            // Map to Bedrock Mistral format
            var mistralRequest = new BedrockMistralChatRequest
            {
                Prompt = BuildMistralPrompt(request.Messages),
                MaxTokens = request.MaxTokens ?? 512,
                Temperature = request.Temperature.HasValue ? (float)request.Temperature.Value : 0.7f,
                TopP = request.TopP.HasValue ? (float)request.TopP.Value : 0.9f,
                TopK = 50, // Default top-k for Mistral
                Stop = request.Stop?.ToList()
            };

            string modelId = request.Model ?? ProviderModelId;
            using var client = CreateHttpClient(PrimaryKeyCredential.ApiKey);
            string apiUrl = $"model/{modelId}/invoke";

            // Send request with AWS Signature V4 authentication
            var response = await SendBedrockRequestAsync(client, HttpMethod.Post, apiUrl, mistralRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new LLMCommunicationException($"Bedrock API error: {response.StatusCode} - {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bedrockResponse = JsonSerializer.Deserialize<BedrockMistralChatResponse>(responseContent, JsonOptions);

            if (bedrockResponse?.Outputs == null || !bedrockResponse.Outputs.Any())
            {
                throw new LLMCommunicationException("Failed to deserialize the response from AWS Bedrock API or response outputs are empty");
            }

            // Get the first output
            var output = bedrockResponse.Outputs.FirstOrDefault();
            var responseText = output?.Text ?? string.Empty;
            var finishReason = MapMistralStopReason(output?.StopReason);

            // Mistral doesn't provide token counts in the response, so estimate them
            var promptTokens = EstimateTokenCount(mistralRequest.Prompt);
            var completionTokens = EstimateTokenCount(responseText);

            // Map to standard format
            return new ChatCompletionResponse
            {
                Id = $"bedrock-{Guid.NewGuid()}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = modelId,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        Message = new ConduitLLM.Core.Models.Message
                        {
                            Role = "assistant",
                            Content = responseText
                        },
                        FinishReason = finishReason,
                        Logprobs = null
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = promptTokens + completionTokens
                },
                SystemFingerprint = null
            };
        }
    }
}