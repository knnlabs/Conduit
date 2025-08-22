using System.Text.Json;
using System.Text.Json.Serialization;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Translators
{
    /// <summary>
    /// Translates messages between Conduit's unified format and ElevenLabs Conversational AI format.
    /// </summary>
    /// <remarks>
    /// ElevenLabs Conversational AI provides real-time voice interactions with
    /// focus on high-quality voice synthesis and natural conversation flow.
    /// </remarks>
    public class ElevenLabsRealtimeTranslator : IRealtimeMessageTranslator
    {
        private readonly ILogger<ElevenLabsRealtimeTranslator> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public string Provider => "ElevenLabs";

        public ElevenLabsRealtimeTranslator(ILogger<ElevenLabsRealtimeTranslator> logger)
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
            // Map Conduit messages to ElevenLabs format
            object elevenLabsMessage = message switch
            {
                RealtimeAudioFrame audioFrame => new
                {
                    type = "audio_input",
                    audio = new
                    {
                        data = Convert.ToBase64String(audioFrame.AudioData),
                        format = "pcm",
                        sample_rate = 16000,
                        channels = 1
                    }
                },

                RealtimeTextInput textInput => new
                {
                    type = "text_input",
                    text = textInput.Text,
                    metadata = new
                    {
                        role = "user"
                    }
                },

                RealtimeFunctionResponse funcResponse => new
                {
                    type = "tool_response",
                    tool_call_id = funcResponse.CallId,
                    output = funcResponse.Output
                },

                RealtimeResponseRequest responseRequest => new
                {
                    type = "generate_response",
                    config = new
                    {
                        instructions = responseRequest.Instructions,
                        temperature = responseRequest.Temperature ?? 0.8,
                        voice_settings = new
                        {
                            stability = 0.5,
                            similarity_boost = 0.75
                        }
                    }
                },

                _ => throw new NotSupportedException($"Message type '{message.GetType().Name}' is not supported by ElevenLabs")
            };

            var json = JsonSerializer.Serialize(elevenLabsMessage, _jsonOptions);
            _logger.LogDebug("Translated to ElevenLabs: {MessageType} -> {Json}", message.GetType().Name, json);

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
                    throw new InvalidOperationException("ElevenLabs message missing 'type' field");
                }

                var messageType = typeElement.GetString();
                _logger.LogDebug("Translating from ElevenLabs: {MessageType}", messageType);

                switch (messageType)
                {
                    case "conversation_started":
                    case "conversation_updated":
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = messageType.Replace("conversation_", "session_"),
                            Details = providerMessage
                        });
                        break;

                    case "audio_output":
                        if (root.TryGetProperty("audio", out var audio) &&
                            audio.TryGetProperty("data", out var audioData))
                        {
                            var audioBytes = Convert.FromBase64String(audioData.GetString() ?? "");
                            messages.Add(new RealtimeAudioFrame
                            {
                                AudioData = audioBytes,
                                IsOutput = true
                            });
                        }
                        break;

                    case "text_output":
                        if (root.TryGetProperty("text", out var text))
                        {
                            messages.Add(new RealtimeTextOutput
                            {
                                Text = text.GetString() ?? "",
                                IsDelta = root.TryGetProperty("is_partial", out var partial) && partial.GetBoolean()
                            });
                        }
                        break;

                    case "tool_call":
                        messages.Add(new RealtimeFunctionCall
                        {
                            CallId = root.GetProperty("tool_call_id").GetString() ?? "",
                            Name = root.GetProperty("tool_name").GetString(),
                            Arguments = root.GetProperty("arguments").GetRawText(),
                            IsDelta = false
                        });
                        break;

                    case "turn_complete":
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = "response_complete"
                        });

                        // ElevenLabs includes metrics in turn_complete
                        if (root.TryGetProperty("metrics", out var metrics))
                        {
                            _logger.LogDebug("ElevenLabs metrics: {Metrics}", metrics.GetRawText());

                            // Extract character count for cost estimation
                            if (metrics.TryGetProperty("characters_synthesized", out var chars))
                            {
                                // Store this for usage tracking
                                messages.Add(new RealtimeStatusMessage
                                {
                                    Status = "usage_update",
                                    Details = JsonSerializer.Serialize(new
                                    {
                                        characters = chars.GetInt32(),
                                        duration_ms = metrics.TryGetProperty("duration_ms", out var duration) ? duration.GetInt32() : 0
                                    })
                                });
                            }
                        }
                        break;

                    case "error":
                        var error = ParseError(root);
                        messages.Add(new RealtimeErrorMessage
                        {
                            Error = error
                        });
                        break;

                    case "interruption":
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = "interrupted",
                            Details = providerMessage
                        });
                        break;

                    default:
                        _logger.LogWarning("Unknown ElevenLabs message type: {Type}", messageType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ElevenLabs message: {Message}", providerMessage);
                throw new InvalidOperationException("Failed to parse ElevenLabs realtime message", ex);
            }

            return await Task.FromResult(messages);
        }

        public async Task<TranslationValidationResult> ValidateSessionConfigAsync(RealtimeSessionConfig config)
        {
            var result = new TranslationValidationResult { IsValid = true };

            // Validate model/agent
            var supportedAgents = new[] { "conversational-v1", "rachel", "sam", "charlie" };
            if (!string.IsNullOrEmpty(config.Model) && !supportedAgents.Contains(config.Model))
            {
                result.Warnings.Add($"Agent '{config.Model}' may not be available. Known agents: {string.Join(", ", supportedAgents)}");
            }

            // Validate voice
            var supportedVoices = new[] { "rachel", "sam", "charlie", "emily", "adam", "elli", "josh" };
            if (!string.IsNullOrEmpty(config.Voice) && !supportedVoices.Contains(config.Voice.ToLowerInvariant()))
            {
                result.Warnings.Add($"Voice '{config.Voice}' may not be available. Known voices: {string.Join(", ", supportedVoices)}");
            }

            // Validate audio formats
            var supportedFormats = new[] { RealtimeAudioFormat.PCM16_16kHz };
            if (!supportedFormats.Contains(config.InputFormat))
            {
                result.Errors.Add($"Input format '{config.InputFormat}' is not supported by ElevenLabs. Use PCM16 at 16kHz.");
                result.IsValid = false;
            }

            // ElevenLabs specific requirements
            if (config.TurnDetection.Enabled)
            {
                result.Warnings.Add("ElevenLabs handles turn detection automatically based on voice activity");
            }

            return await Task.FromResult(result);
        }

        public async Task<string> TransformSessionConfigAsync(RealtimeSessionConfig config)
        {
            var elevenLabsConfig = new
            {
                type = "conversation_config",
                config = new
                {
                    agent_id = config.Model ?? "conversational-v1",
                    voice_id = MapVoiceId(config.Voice),
                    system_prompt = config.SystemPrompt,
                    language = config.Language ?? "en",
                    voice_settings = new
                    {
                        stability = 0.5,
                        similarity_boost = 0.75,
                        style = 0.0,
                        use_speaker_boost = true
                    },
                    generation_config = new
                    {
                        temperature = config.Temperature ?? 0.8,
                        response_format = "audio", // or "text" or "both"
                        enable_ssml = false
                    },
                    audio_config = new
                    {
                        input_format = "pcm_16000",
                        output_format = "pcm_16000",
                        encoding = "pcm_s16le"
                    },
                    interruption_config = new
                    {
                        enabled = true,
                        threshold_ms = 500
                    }
                }
            };

            // Add tools/functions if configured
            if (config.Tools != null && config.Tools.Count() > 0)
            {
                var tools = config.Tools.Select(t => new
                {
                    name = t.Function?.Name,
                    description = t.Function?.Description,
                    parameters = t.Function?.Parameters
                }).ToList();

                ((dynamic)elevenLabsConfig.config).tools = tools;
            }

            return await Task.FromResult(JsonSerializer.Serialize(elevenLabsConfig, _jsonOptions));
        }

        public string? GetRequiredSubprotocol()
        {
            return null; // ElevenLabs doesn't require a specific subprotocol
        }

        public async Task<Dictionary<string, string>> GetConnectionHeadersAsync(RealtimeSessionConfig config)
        {
            var headers = new Dictionary<string, string>
            {
                ["X-ElevenLabs-Version"] = "v1",
                ["X-Client-Info"] = "conduit-llm/1.0"
            };

            return await Task.FromResult(headers);
        }

        public async Task<IEnumerable<string>> GetInitializationMessagesAsync(RealtimeSessionConfig config)
        {
            var messages = new List<string>();

            // Send configuration
            var sessionConfig = await TransformSessionConfigAsync(config);
            messages.Add(sessionConfig);

            // Start the conversation
            messages.Add(JsonSerializer.Serialize(new
            {
                type = "conversation_start"
            }, _jsonOptions));

            return messages;
        }

        public RealtimeError TranslateError(string providerError)
        {
            try
            {
                using var doc = JsonDocument.Parse(providerError);
                var root = doc.RootElement;

                string? code = null;
                string? message = null;

                if (root.TryGetProperty("error", out var errorElement))
                {
                    code = errorElement.TryGetProperty("code", out var codeElem) ? codeElem.GetString() : null;
                    message = errorElement.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : null;
                }
                else if (root.TryGetProperty("code", out var codeElem))
                {
                    code = codeElem.GetString();
                    message = root.TryGetProperty("message", out var msgElem) ? msgElem.GetString() : providerError;
                }

                return new RealtimeError
                {
                    Code = code ?? "unknown",
                    Message = message ?? "Unknown error",
                    Severity = DetermineErrorSeverity(code),
                    IsTerminal = IsTerminalError(code),
                    RetryAfterMs = code == "rate_limit_exceeded" ? 60000 : null
                };
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

        private string MapVoiceId(string? voiceName)
        {
            if (string.IsNullOrEmpty(voiceName))
                return "21m00Tcm4TlvDq8ikWAM"; // Default Rachel voice ID

            // Map common voice names to ElevenLabs voice IDs
            return voiceName.ToLowerInvariant() switch
            {
                "rachel" => "21m00Tcm4TlvDq8ikWAM",
                "sam" => "yoZ06aMxZJJ28mfd3POQ",
                "charlie" => "IKne3meq5aSn9XLyUdCD",
                "emily" => "LcfcDJNUP1GQjkzn1xUU",
                "adam" => "pNInz6obpgDQGcFmaJgB",
                "elli" => "MF3mGyEYCl7XYWbV9V6O",
                "josh" => "TxGEqnHWrfWFTfGW9XjX",
                _ => "21m00Tcm4TlvDq8ikWAM" // Default to Rachel
            };
        }

        private RealtimeError ParseError(JsonElement root)
        {
            var error = root.TryGetProperty("error", out var errorElem) ? errorElem : root;

            return new RealtimeError
            {
                Code = error.TryGetProperty("code", out var code) ? code.GetString() ?? "unknown" : "unknown",
                Message = error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error",
                Severity = Core.Interfaces.ErrorSeverity.Error,
                IsTerminal = false,
                Details = error.TryGetProperty("details", out var details) ?
                    JsonSerializer.Deserialize<Dictionary<string, object>>(details.GetRawText()) : null
            };
        }

        private Core.Interfaces.ErrorSeverity DetermineErrorSeverity(string? code)
        {
            return code switch
            {
                "invalid_request" => Core.Interfaces.ErrorSeverity.Error,
                "authentication_error" => Core.Interfaces.ErrorSeverity.Critical,
                "rate_limit_exceeded" => Core.Interfaces.ErrorSeverity.Warning,
                "server_error" => Core.Interfaces.ErrorSeverity.Critical,
                "voice_not_found" => Core.Interfaces.ErrorSeverity.Error,
                _ => Core.Interfaces.ErrorSeverity.Error
            };
        }

        private bool IsTerminalError(string? code)
        {
            return code switch
            {
                "authentication_error" => true,
                "invalid_api_key" => true,
                "subscription_expired" => true,
                "quota_exceeded" => true,
                _ => false
            };
        }
    }
}
