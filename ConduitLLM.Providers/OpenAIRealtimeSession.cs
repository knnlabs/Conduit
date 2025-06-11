using System;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Providers.Translators;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// OpenAI-specific implementation of a real-time audio session.
    /// </summary>
    public class OpenAIRealtimeSession : RealtimeSession
    {
        private readonly string _url;
        private readonly string _apiKey;
        private readonly RealtimeSessionConfig _config;
        private readonly ILogger _logger;
        private readonly ClientWebSocket _webSocket;
        private readonly OpenAIRealtimeTranslatorV2 _translator;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _receiveTask;

        public OpenAIRealtimeSession(
            string url,
            string apiKey,
            RealtimeSessionConfig config,
            ILogger logger)
        {
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webSocket = new ClientWebSocket();
            _translator = new OpenAIRealtimeTranslatorV2(logger as ILogger<OpenAIRealtimeTranslatorV2> ??
                throw new ArgumentException("Logger must be ILogger<OpenAIRealtimeTranslatorV2>"));
            _cancellationTokenSource = new CancellationTokenSource();

            // Set base class properties
            Provider = "OpenAI";
            Config = config;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            // Add required headers
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

            // Set subprotocol if required
            var subprotocol = _translator.GetRequiredSubprotocol();
            if (!string.IsNullOrEmpty(subprotocol))
            {
                _webSocket.Options.AddSubProtocol(subprotocol);
            }

            // Add any custom headers
            var headers = await _translator.GetConnectionHeadersAsync(_config);
            foreach (var header in headers)
            {
                _webSocket.Options.SetRequestHeader(header.Key, header.Value);
            }

            try
            {
                State = SessionState.Connecting;
                await _webSocket.ConnectAsync(new Uri(_url), cancellationToken);
                _logger.LogInformation("Connected to OpenAI Realtime API at {Url}", _url);

                State = SessionState.Connected;

                // Send initialization messages
                var initMessages = await _translator.GetInitializationMessagesAsync(_config);
                foreach (var message in initMessages)
                {
                    await SendRawMessageAsync(message, cancellationToken);
                }

                // Start receive loop
                _receiveTask = ReceiveLoopAsync(_cancellationTokenSource.Token);

                State = SessionState.Active;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to OpenAI Realtime API");
                State = SessionState.Error;
                throw;
            }
        }

        public async Task SendMessageAsync(RealtimeMessage message, CancellationToken cancellationToken = default)
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not open");
            }

            var jsonMessage = await _translator.TranslateToProviderAsync(message);
            await SendRawMessageAsync(jsonMessage, cancellationToken);
        }


        public async IAsyncEnumerable<RealtimeMessage> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);

            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server closed connection",
                        cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                    _logger.LogDebug("Received message from OpenAI: {Message}", message);

                    var conduitMessages = await _translator.TranslateFromProviderAsync(message);
                    foreach (var conduitMessage in conduitMessages)
                    {
                        yield return conduitMessage;
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource.Cancel();

                if (_receiveTask != null)
                {
                    try
                    {
                        _receiveTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch { }
                }

                if (_webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None)
                            .Wait(TimeSpan.FromSeconds(5));
                    }
                    catch { }
                }

                _webSocket.Dispose();
                _cancellationTokenSource.Dispose();
            }

            base.Dispose(disposing);
        }

        public async Task SendRawMessageAsync(string message, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            _logger.LogDebug("Sent message to OpenAI: {Message}", message);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);

            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Server closed connection",
                            cancellationToken);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array!, 0, result.Count);
                        _logger.LogDebug("Received message from OpenAI: {Message}", message);

                        try
                        {
                            var conduitMessages = await _translator.TranslateFromProviderAsync(message);
                            foreach (var conduitMessage in conduitMessages)
                            {
                                // Messages will be processed by the caller
                                _logger.LogDebug("Translated message type: {Type}", conduitMessage.Type);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error translating provider message");
                            // Error handling will be done by the caller
                        }
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error in receive loop");
                State = SessionState.Error;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in receive loop");
                State = SessionState.Error;
            }
            finally
            {
                State = SessionState.Closed;
                _logger.LogInformation("Connection closed");
            }
        }
    }
}
