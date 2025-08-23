using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Translators
{
    /// <summary>
    /// Translates messages between Conduit's unified format and OpenAI's Realtime API format.
    /// Simplified version that works with the actual model structure.
    /// </summary>
    public class OpenAIRealtimeTranslatorV2 : IRealtimeMessageTranslator
    {
        private readonly ILogger<OpenAIRealtimeTranslatorV2> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public string Provider => "OpenAI";

        public OpenAIRealtimeTranslatorV2(ILogger<OpenAIRealtimeTranslatorV2> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
            };
        }

        public async Task<string> TranslateToProviderAsync(RealtimeMessage message)
        {
            // Map common Conduit message types to OpenAI format
            object openAiMessage = message switch
            {
                RealtimeAudioFrame audioFrame => new
                {
                    type = "input_audio_buffer.append",
                    audio = Convert.ToBase64String(audioFrame.AudioData)
                },

                RealtimeTextInput textInput => new
                {
                    type = "conversation.item.create",
                    item = new
                    {
                        type = "message",
                        role = "user",
                        content = new[]
                        {
                            new { type = "input_text", text = textInput.Text }
                        }
                    }
                },

                RealtimeFunctionResponse funcResponse => new
                {
                    type = "conversation.item.create",
                    item = new
                    {
                        type = "function_call_output",
                        call_id = funcResponse.CallId,
                        output = funcResponse.Output
                    }
                },

                RealtimeResponseRequest responseRequest => new
                {
                    type = "response.create",
                    response = new
                    {
                        modalities = new[] { "text", "audio" },
                        instructions = responseRequest.Instructions,
                        temperature = responseRequest.Temperature
                    }
                },

                _ => throw new NotSupportedException($"Message type '{message.GetType().Name}' is not supported")
            };

            var json = JsonSerializer.Serialize(openAiMessage, _jsonOptions);
            _logger.LogDebug("Translated to OpenAI: {MessageType} -> {Json}", message.GetType().Name, json);

            return await Task.FromResult(json);
        }

        public async Task<IEnumerable<RealtimeMessage>> TranslateFromProviderAsync(string providerMessage)
        {
            var messages = new List<RealtimeMessage>();

            try
            {
                using var doc = JsonDocument.Parse(providerMessage);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeElement))
                {
                    throw new InvalidOperationException("OpenAI message missing 'type' field");
                }

                var messageType = typeElement.GetString();
                _logger.LogDebug("Translating from OpenAI: {MessageType}", messageType);

                switch (messageType)
                {
                    case "session.created":
                    case "session.updated":
                        // Session events - could map to status messages
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = "session_updated",
                            Details = providerMessage
                        });
                        break;

                    case "response.audio.delta":
                        // Audio chunk from AI
                        if (root.TryGetProperty("delta", out var audioDelta))
                        {
                            var audioData = Convert.FromBase64String(audioDelta.GetString() ?? "");
                            messages.Add(new RealtimeAudioFrame
                            {
                                AudioData = audioData,
                                IsOutput = true
                            });
                        }
                        break;

                    case "response.text.delta":
                        // Text chunk from AI
                        if (root.TryGetProperty("delta", out var textDelta))
                        {
                            messages.Add(new RealtimeTextOutput
                            {
                                Text = textDelta.GetString() ?? "",
                                IsDelta = true
                            });
                        }
                        break;

                    case "response.function_call_arguments.delta":
                        // Function call in progress
                        if (root.TryGetProperty("call_id", out var callId) &&
                            root.TryGetProperty("delta", out var argsDelta))
                        {
                            messages.Add(new RealtimeFunctionCall
                            {
                                CallId = callId.GetString() ?? "",
                                Arguments = argsDelta.GetString() ?? "",
                                IsDelta = true
                            });
                        }
                        break;

                    case "response.done":
                        // Response completed
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = "response_complete"
                        });
                        break;

                    case "error":
                        // Error from provider
                        var error = ParseError(root);
                        messages.Add(new RealtimeErrorMessage
                        {
                            Error = error
                        });
                        break;

                    default:
                        _logger.LogWarning("Unknown OpenAI message type: {Type}", messageType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing OpenAI message: {Message}", providerMessage);
                throw new InvalidOperationException("Failed to parse OpenAI realtime message", ex);
            }

            return await Task.FromResult(messages);
        }

        public async Task<TranslationValidationResult> ValidateSessionConfigAsync(RealtimeSessionConfig config)
        {
            var result = new TranslationValidationResult { IsValid = true };

            // Validate model
            var supportedModels = new[] { "gpt-4o-realtime-preview", "gpt-4o-realtime-preview-2024-10-01" };
            if (!string.IsNullOrEmpty(config.Model) && !supportedModels.Contains(config.Model))
            {
                result.Errors.Add($"Model '{config.Model}' is not supported. Use: {string.Join(", ", supportedModels)}");
                result.IsValid = false;
            }

            // Validate voice
            var supportedVoices = new[] { "alloy", "echo", "shimmer" };
            if (!string.IsNullOrEmpty(config.Voice) && !supportedVoices.Contains(config.Voice))
            {
                result.Warnings.Add($"Voice '{config.Voice}' may not be supported. Recommended: {string.Join(", ", supportedVoices)}");
            }

            // Validate audio formats
            var supportedFormats = new[] { RealtimeAudioFormat.PCM16_16kHz, RealtimeAudioFormat.PCM16_24kHz, RealtimeAudioFormat.G711_ULAW };
            if (!supportedFormats.Contains(config.InputFormat))
            {
                result.Errors.Add($"Input format '{config.InputFormat}' is not supported by OpenAI");
                result.IsValid = false;
            }

            return await Task.FromResult(result);
        }

        public async Task<string> TransformSessionConfigAsync(RealtimeSessionConfig config)
        {
            var openAiConfig = new
            {
                type = "session.update",
                session = new
                {
                    model = config.Model ?? "gpt-4o-realtime-preview",
                    voice = config.Voice ?? "alloy",
                    instructions = config.SystemPrompt,
                    input_audio_format = MapAudioFormat(config.InputFormat),
                    output_audio_format = MapAudioFormat(config.OutputFormat),
                    input_audio_transcription = config.Transcription?.EnableUserTranscription == true ? new
                    {
                        model = "whisper-1"
                    } : null,
                    turn_detection = config.TurnDetection.Enabled ? new
                    {
                        type = config.TurnDetection.Type.ToString().ToLowerInvariant(),
                        threshold = config.TurnDetection.Threshold,
                        prefix_padding_ms = config.TurnDetection.PrefixPaddingMs,
                        silence_duration_ms = config.TurnDetection.SilenceThresholdMs
                    } : null,
                    temperature = config.Temperature,
                    modalities = new[] { "text", "audio" }
                }
            };

            return await Task.FromResult(JsonSerializer.Serialize(openAiConfig, _jsonOptions));
        }

        public string? GetRequiredSubprotocol()
        {
            return "openai-beta.realtime-v1";
        }

        public async Task<Dictionary<string, string>> GetConnectionHeadersAsync(RealtimeSessionConfig config)
        {
            var headers = new Dictionary<string, string>
            {
                ["OpenAI-Beta"] = "realtime=v1"
            };

            return await Task.FromResult(headers);
        }

        public async Task<IEnumerable<string>> GetInitializationMessagesAsync(RealtimeSessionConfig config)
        {
            var messages = new List<string>();

            // Send session configuration as first message
            var sessionConfig = await TransformSessionConfigAsync(config);
            messages.Add(sessionConfig);

            return messages;
        }

        public RealtimeError TranslateError(string providerError)
        {
            try
            {
                using var doc = JsonDocument.Parse(providerError);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorElement))
                {
                    var code = errorElement.TryGetProperty("code", out var codeElem) ? codeElem.GetString() : "unknown";
                    var message = errorElement.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : providerError;

                    return new RealtimeError
                    {
                        Code = code ?? "unknown",
                        Message = message ?? "Unknown error",
                        Severity = DetermineErrorSeverity(code),
                        IsTerminal = IsTerminalError(code)
                    };
                }
            }
            catch
            {
                // If we can't parse it, treat as generic error
            }

            return new RealtimeError
            {
                Code = "provider_error",
                Message = providerError,
                Severity = Core.Interfaces.ErrorSeverity.Error,
                IsTerminal = false
            };
        }

        private string MapAudioFormat(RealtimeAudioFormat format)
        {
            return format switch
            {
                RealtimeAudioFormat.PCM16_16kHz => "pcm16",
                RealtimeAudioFormat.PCM16_24kHz => "pcm16",
                RealtimeAudioFormat.G711_ULAW => "g711_ulaw",
                RealtimeAudioFormat.G711_ALAW => "g711_alaw",
                _ => "pcm16" // Default
            };
        }

        private RealtimeError ParseError(JsonElement root)
        {
            var error = root.GetProperty("error");

            return new RealtimeError
            {
                Code = error.TryGetProperty("code", out var code) ? code.GetString() ?? "unknown" : "unknown",
                Message = error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error",
                Severity = Core.Interfaces.ErrorSeverity.Error,
                IsTerminal = false
            };
        }

        private Core.Interfaces.ErrorSeverity DetermineErrorSeverity(string? code)
        {
            return code switch
            {
                "invalid_request_error" => Core.Interfaces.ErrorSeverity.Error,
                "server_error" => Core.Interfaces.ErrorSeverity.Critical,
                "rate_limit_error" => Core.Interfaces.ErrorSeverity.Warning,
                _ => Core.Interfaces.ErrorSeverity.Error
            };
        }

        private bool IsTerminalError(string? code)
        {
            return code switch
            {
                "invalid_api_key" => true,
                "insufficient_quota" => true,
                "server_error" => false,
                "rate_limit_error" => false,
                _ => false
            };
        }
    }

    // Additional message types used by the translator
    public class RealtimeTextInput : RealtimeMessage
    {
        public override string Type => "text_input";
        public string Text { get; set; } = "";
    }

    public class RealtimeTextOutput : RealtimeMessage
    {
        public override string Type => "text_output";
        public string Text { get; set; } = "";
        public bool IsDelta { get; set; }
    }

    public class RealtimeFunctionCall : RealtimeMessage
    {
        public override string Type => "function_call";
        public string CallId { get; set; } = "";
        public string? Name { get; set; }
        public string Arguments { get; set; } = "";
        public bool IsDelta { get; set; }
    }

    public class RealtimeFunctionResponse : RealtimeMessage
    {
        public override string Type => "function_response";
        public string CallId { get; set; } = "";
        public string Output { get; set; } = "";
    }

    public class RealtimeResponseRequest : RealtimeMessage
    {
        public override string Type => "response_request";
        public string? Instructions { get; set; }
        public double? Temperature { get; set; }
    }

    public class RealtimeStatusMessage : RealtimeMessage
    {
        public override string Type => "status";
        public string Status { get; set; } = "";
        public string? Details { get; set; }
    }

    public class RealtimeErrorMessage : RealtimeMessage
    {
        public override string Type => "error";
        public RealtimeError Error { get; set; } = new();
    }
}
