using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for translating real-time messages between
    /// Conduit's unified format and provider-specific formats.
    /// </summary>
    /// <remarks>
    /// Each provider (OpenAI, Ultravox, ElevenLabs) has different message
    /// formats and protocols. This interface allows for bidirectional
    /// translation while maintaining a consistent API for clients.
    /// </remarks>
    public interface IRealtimeMessageTranslator
    {
        /// <summary>
        /// Gets the provider this translator handles.
        /// </summary>
        string Provider { get; }

        /// <summary>
        /// Translates a message from Conduit format to provider format.
        /// </summary>
        /// <param name="message">The Conduit-format message.</param>
        /// <returns>Provider-specific message data (usually JSON string).</returns>
        /// <exception cref="NotSupportedException">Thrown when message type is not supported by provider.</exception>
        Task<string> TranslateToProviderAsync(RealtimeMessage message);

        /// <summary>
        /// Translates a message from provider format to Conduit format.
        /// </summary>
        /// <param name="providerMessage">The provider-specific message data.</param>
        /// <returns>One or more Conduit-format messages (providers may send compound messages).</returns>
        /// <exception cref="InvalidOperationException">Thrown when provider message is malformed.</exception>
        Task<IEnumerable<RealtimeMessage>> TranslateFromProviderAsync(string providerMessage);

        /// <summary>
        /// Validates that a session configuration is supported by the provider.
        /// </summary>
        /// <param name="config">The session configuration to validate.</param>
        /// <returns>Validation result with any warnings or errors.</returns>
        Task<TranslationValidationResult> ValidateSessionConfigAsync(RealtimeSessionConfig config);

        /// <summary>
        /// Transforms a session configuration to provider-specific format.
        /// </summary>
        /// <param name="config">The Conduit session configuration.</param>
        /// <returns>Provider-specific configuration data.</returns>
        Task<string> TransformSessionConfigAsync(RealtimeSessionConfig config);

        /// <summary>
        /// Gets the WebSocket subprotocol required by the provider, if any.
        /// </summary>
        /// <returns>Subprotocol string or null if not required.</returns>
        string? GetRequiredSubprotocol();

        /// <summary>
        /// Gets custom headers required for the WebSocket connection.
        /// </summary>
        /// <param name="config">The session configuration.</param>
        /// <returns>Dictionary of header names and values.</returns>
        Task<Dictionary<string, string>> GetConnectionHeadersAsync(RealtimeSessionConfig config);

        /// <summary>
        /// Handles provider-specific connection initialization.
        /// </summary>
        /// <param name="config">The session configuration.</param>
        /// <returns>Initial messages to send after connection.</returns>
        Task<IEnumerable<string>> GetInitializationMessagesAsync(RealtimeSessionConfig config);

        /// <summary>
        /// Maps provider-specific error codes to Conduit error types.
        /// </summary>
        /// <param name="providerError">The provider error message or code.</param>
        /// <returns>Standardized error information.</returns>
        RealtimeError TranslateError(string providerError);
    }

    /// <summary>
    /// Result of validating a configuration for translation.
    /// </summary>
    public class TranslationValidationResult
    {
        /// <summary>
        /// Whether the configuration is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Any validation errors.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Any warnings (non-fatal issues).
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Suggested configuration adjustments.
        /// </summary>
        public Dictionary<string, object>? SuggestedAdjustments { get; set; }
    }

    /// <summary>
    /// Standardized error information for real-time connections.
    /// </summary>
    public class RealtimeError
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Error severity.
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Whether the connection should be terminated.
        /// </summary>
        public bool IsTerminal { get; set; }

        /// <summary>
        /// Suggested retry delay in milliseconds, if applicable.
        /// </summary>
        public int? RetryAfterMs { get; set; }

        /// <summary>
        /// Additional error details from the provider.
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// Severity levels for real-time errors.
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Informational, not an actual error.
        /// </summary>
        Info,

        /// <summary>
        /// Warning that doesn't affect functionality.
        /// </summary>
        Warning,

        /// <summary>
        /// Error that affects some functionality.
        /// </summary>
        Error,

        /// <summary>
        /// Critical error requiring immediate action.
        /// </summary>
        Critical
    }
}
