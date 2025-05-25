using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Models.Audio;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Translators
{
    /// <summary>
    /// Translates messages between Conduit's unified format and Ultravox's real-time API format.
    /// </summary>
    /// <remarks>
    /// Ultravox uses a different message structure than OpenAI, with focus on
    /// low-latency voice interactions and streamlined message types.
    /// </remarks>
    public class UltravoxRealtimeTranslator : IRealtimeMessageTranslator
    {
        private readonly ILogger<UltravoxRealtimeTranslator> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public string Provider => "Ultravox";

        public UltravoxRealtimeTranslator(ILogger<UltravoxRealtimeTranslator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
        }

        public async Task<string> TranslateToProviderAsync(RealtimeMessage message)
        {
            // Map Conduit messages to Ultravox format
            object ultravoxMessage = message switch
            {
                RealtimeAudioFrame audioFrame => new
                {
                    type = "audio",
                    data = new
                    {
                        audio = Convert.ToBase64String(audioFrame.AudioData),
                        sampleRate = 24000, // Default to 24kHz
                        channels = 1
                    }
                },
                
                RealtimeTextInput textInput => new
                {
                    type = "text",
                    data = new
                    {
                        text = textInput.Text,
                        role = "user"
                    }
                },
                
                RealtimeFunctionResponse funcResponse => new
                {
                    type = "function_result",
                    data = new
                    {
                        callId = funcResponse.CallId,
                        result = funcResponse.Output
                    }
                },
                
                RealtimeResponseRequest responseRequest => new
                {
                    type = "generate",
                    data = new
                    {
                        prompt = responseRequest.Instructions,
                        temperature = responseRequest.Temperature ?? 0.7,
                        maxTokens = 4096
                    }
                },
                
                _ => throw new NotSupportedException($"Message type '{message.GetType().Name}' is not supported by Ultravox")
            };

            var json = JsonSerializer.Serialize(ultravoxMessage, _jsonOptions);
            _logger.LogDebug("Translated to Ultravox: {MessageType} -> {Json}", message.GetType().Name, json);
            
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
                    throw new InvalidOperationException("Ultravox message missing 'type' field");
                }
                
                var messageType = typeElement.GetString();
                _logger.LogDebug("Translating from Ultravox: {MessageType}", messageType);
                
                switch (messageType)
                {
                    case "session_started":
                    case "session_updated":
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = messageType,
                            Details = providerMessage
                        });
                        break;
                        
                    case "audio_chunk":
                        if (root.TryGetProperty("data", out var audioData) &&
                            audioData.TryGetProperty("audio", out var audioBase64))
                        {
                            var audioBytes = Convert.FromBase64String(audioBase64.GetString() ?? "");
                            messages.Add(new RealtimeAudioFrame
                            {
                                AudioData = audioBytes,
                                IsOutput = true
                            });
                        }
                        break;
                        
                    case "text_chunk":
                        if (root.TryGetProperty("data", out var textData) &&
                            textData.TryGetProperty("text", out var text))
                        {
                            messages.Add(new RealtimeTextOutput
                            {
                                Text = text.GetString() ?? "",
                                IsDelta = true
                            });
                        }
                        break;
                        
                    case "function_call":
                        if (root.TryGetProperty("data", out var funcData))
                        {
                            messages.Add(new RealtimeFunctionCall
                            {
                                CallId = funcData.GetProperty("callId").GetString() ?? "",
                                Name = funcData.GetProperty("name").GetString(),
                                Arguments = funcData.GetProperty("arguments").GetRawText(),
                                IsDelta = false
                            });
                        }
                        break;
                        
                    case "generation_complete":
                        messages.Add(new RealtimeStatusMessage
                        {
                            Status = "response_complete"
                        });
                        
                        // Check for usage stats
                        if (root.TryGetProperty("usage", out var usage))
                        {
                            // Ultravox may provide usage differently
                            _logger.LogDebug("Ultravox usage data: {Usage}", usage.GetRawText());
                        }
                        break;
                        
                    case "error":
                        var error = ParseError(root);
                        messages.Add(new RealtimeErrorMessage
                        {
                            Error = error
                        });
                        break;
                        
                    default:
                        _logger.LogWarning("Unknown Ultravox message type: {Type}", messageType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Ultravox message: {Message}", providerMessage);
                throw new InvalidOperationException("Failed to parse Ultravox realtime message", ex);
            }
            
            return await Task.FromResult(messages);
        }

        public async Task<TranslationValidationResult> ValidateSessionConfigAsync(RealtimeSessionConfig config)
        {
            var result = new TranslationValidationResult { IsValid = true };
            
            // Validate model
            var supportedModels = new[] { "ultravox", "ultravox-v2", "ultravox-realtime" };
            if (!string.IsNullOrEmpty(config.Model) && !supportedModels.Contains(config.Model))
            {
                result.Errors.Add($"Model '{config.Model}' is not supported. Use: {string.Join(", ", supportedModels)}");
                result.IsValid = false;
            }
            
            // Validate audio formats
            var supportedFormats = new[] { RealtimeAudioFormat.PCM16_16kHz, RealtimeAudioFormat.PCM16_24kHz };
            if (!supportedFormats.Contains(config.InputFormat))
            {
                result.Errors.Add($"Input format '{config.InputFormat}' is not supported by Ultravox");
                result.IsValid = false;
            }
            
            // Ultravox specific validations
            if (config.TurnDetection.Enabled && config.TurnDetection.Type != TurnDetectionType.ServerVAD)
            {
                result.Warnings.Add("Ultravox only supports server-side VAD turn detection");
            }
            
            return await Task.FromResult(result);
        }

        public async Task<string> TransformSessionConfigAsync(RealtimeSessionConfig config)
        {
            var ultravoxConfig = new
            {
                type = "session_config",
                data = new
                {
                    model = config.Model ?? "ultravox-v2",
                    systemPrompt = config.SystemPrompt,
                    audioConfig = new
                    {
                        inputFormat = MapAudioFormat(config.InputFormat),
                        outputFormat = MapAudioFormat(config.OutputFormat),
                        sampleRate = GetSampleRate(config.InputFormat),
                        channels = 1 // Ultravox typically uses mono
                    },
                    turnDetection = config.TurnDetection.Enabled ? new
                    {
                        enabled = true,
                        vadThreshold = config.TurnDetection.Threshold,
                        silenceDurationMs = config.TurnDetection.SilenceThresholdMs
                    } : null,
                    responseConfig = new
                    {
                        temperature = config.Temperature,
                        voice = config.Voice ?? "nova", // Ultravox default voice
                        speed = 1.0
                    }
                }
            };
            
            return await Task.FromResult(JsonSerializer.Serialize(ultravoxConfig, _jsonOptions));
        }

        public string? GetRequiredSubprotocol()
        {
            return "ultravox.v1";
        }

        public async Task<Dictionary<string, string>> GetConnectionHeadersAsync(RealtimeSessionConfig config)
        {
            var headers = new Dictionary<string, string>
            {
                ["X-Ultravox-Version"] = "1.0",
                ["X-Ultravox-Client"] = "conduit-llm"
            };
            
            return await Task.FromResult(headers);
        }

        public async Task<IEnumerable<string>> GetInitializationMessagesAsync(RealtimeSessionConfig config)
        {
            var messages = new List<string>();
            
            // Send session configuration
            var sessionConfig = await TransformSessionConfigAsync(config);
            messages.Add(sessionConfig);
            
            // Ultravox starts immediately without additional messages
            
            return messages;
        }

        public RealtimeError TranslateError(string providerError)
        {
            try
            {
                using var doc = JsonDocument.Parse(providerError);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("error", out var errorElement) ||
                    root.TryGetProperty("data", out errorElement))
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
                RealtimeAudioFormat.G711_ULAW => "ulaw",
                RealtimeAudioFormat.G711_ALAW => "alaw",
                _ => "pcm16" // Default
            };
        }

        private int GetSampleRate(RealtimeAudioFormat format)
        {
            return format switch
            {
                RealtimeAudioFormat.PCM16_16kHz => 16000,
                RealtimeAudioFormat.PCM16_24kHz => 24000,
                RealtimeAudioFormat.G711_ULAW => 8000,
                RealtimeAudioFormat.G711_ALAW => 8000,
                _ => 24000 // Default
            };
        }

        private RealtimeError ParseError(JsonElement root)
        {
            var errorData = root.TryGetProperty("data", out var data) ? data : root.GetProperty("error");
            
            return new RealtimeError
            {
                Code = errorData.TryGetProperty("code", out var code) ? code.GetString() ?? "unknown" : "unknown",
                Message = errorData.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error",
                Severity = Core.Interfaces.ErrorSeverity.Error,
                IsTerminal = false
            };
        }

        private Core.Interfaces.ErrorSeverity DetermineErrorSeverity(string? code)
        {
            return code switch
            {
                "invalid_request" => Core.Interfaces.ErrorSeverity.Error,
                "server_error" => Core.Interfaces.ErrorSeverity.Critical,
                "rate_limit" => Core.Interfaces.ErrorSeverity.Warning,
                "authentication_failed" => Core.Interfaces.ErrorSeverity.Critical,
                _ => Core.Interfaces.ErrorSeverity.Error
            };
        }

        private bool IsTerminalError(string? code)
        {
            return code switch
            {
                "authentication_failed" => true,
                "invalid_api_key" => true,
                "quota_exceeded" => true,
                "model_not_available" => true,
                _ => false
            };
        }
    }
}