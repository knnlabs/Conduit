using System.Text;
using System.Text.Json;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Enhanced Server-Sent Events writer that supports multiple event types for streaming responses.
    /// </summary>
    public class EnhancedSSEResponseWriter
    {
        private readonly HttpResponse _response;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _headersWritten;

        public EnhancedSSEResponseWriter(HttpResponse response, JsonSerializerOptions? jsonOptions = null)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Writes SSE headers if not already written.
        /// </summary>
        private async Task EnsureHeadersWrittenAsync()
        {
            if (!_headersWritten)
            {
                _response.ContentType = "text/event-stream";
                _response.Headers.Append("Cache-Control", "no-cache");
                _response.Headers.Append("Connection", "keep-alive");
                _response.Headers.Append("X-Accel-Buffering", "no"); // Disable Nginx buffering
                
                // Add CORS headers if needed
                if (_response.HttpContext.Request.Headers.ContainsKey("Origin"))
                {
                    _response.Headers.Append("Access-Control-Allow-Origin", "*");
                    _response.Headers.Append("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
                    _response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization");
                }
                
                _headersWritten = true;
                await _response.Body.FlushAsync();
            }
        }

        /// <summary>
        /// Writes a content event containing a chat completion chunk.
        /// For OpenAI compatibility, this writes just "data:" without event type.
        /// </summary>
        public async Task WriteContentEventAsync<T>(T data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await EnsureHeadersWrittenAsync();
            
            // OpenAI format uses just "data:" without event type
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var eventData = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(eventData);
            await _response.Body.WriteAsync(bytes, cancellationToken);
            await _response.Body.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Writes a metrics event containing streaming metrics.
        /// </summary>
        public async Task WriteMetricsEventAsync<T>(T metrics, CancellationToken cancellationToken = default)
        {
            await WriteEventAsync("metrics", metrics, cancellationToken);
        }

        /// <summary>
        /// Writes a final metrics event containing complete performance metrics.
        /// </summary>
        public async Task WriteFinalMetricsEventAsync<T>(T metrics, CancellationToken cancellationToken = default)
        {
            await WriteEventAsync("metrics-final", metrics, cancellationToken);
        }

        /// <summary>
        /// Writes an error event.
        /// </summary>
        public async Task WriteErrorEventAsync(string error, CancellationToken cancellationToken = default)
        {
            await WriteEventAsync("error", new { error }, cancellationToken);
        }

        /// <summary>
        /// Writes a generic SSE event with specified event type and data.
        /// </summary>
        public async Task WriteEventAsync<T>(string eventType, T data, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await EnsureHeadersWrittenAsync();
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var eventData = new StringBuilder();
            
            // Add event type if specified
            if (!string.IsNullOrEmpty(eventType))
            {
                eventData.AppendLine($"event: {eventType}");
            }
            
            // Add data field
            eventData.AppendLine($"data: {json}");
            
            // Add empty line to signal end of event
            eventData.AppendLine();
            
            var bytes = Encoding.UTF8.GetBytes(eventData.ToString());
            await _response.Body.WriteAsync(bytes, cancellationToken);
            await _response.Body.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the done event to signal stream completion.
        /// </summary>
        public async Task WriteDoneEventAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await EnsureHeadersWrittenAsync();
            
            // OpenAI format requires just "data: [DONE]" without event type
            var doneData = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await _response.Body.WriteAsync(doneData, cancellationToken);
            await _response.Body.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Writes a keep-alive comment to maintain the connection.
        /// </summary>
        public async Task WriteKeepAliveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await EnsureHeadersWrittenAsync();
            
            var keepAlive = Encoding.UTF8.GetBytes(": keep-alive\n\n");
            await _response.Body.WriteAsync(keepAlive, cancellationToken);
            await _response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Extension methods for enhanced SSE support.
    /// </summary>
    public static class EnhancedSSEExtensions
    {
        /// <summary>
        /// Creates an enhanced SSE response writer for the HTTP response.
        /// </summary>
        public static EnhancedSSEResponseWriter CreateEnhancedSSEWriter(
            this HttpResponse response, 
            JsonSerializerOptions? jsonOptions = null)
        {
            return new EnhancedSSEResponseWriter(response, jsonOptions);
        }
    }
}