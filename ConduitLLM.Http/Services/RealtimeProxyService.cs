using System.Net.WebSockets;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service that proxies WebSocket connections between clients and real-time audio providers.
    /// </summary>
    public partial class RealtimeProxyService : IRealtimeProxyService
    {
        private readonly IRealtimeMessageTranslatorFactory _translatorFactory;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IRealtimeConnectionManager _connectionManager;
        private readonly IRealtimeUsageTracker _usageTracker;
        private readonly ILogger<RealtimeProxyService> _logger;
        
        // Enhanced metrics tracking
        private readonly Dictionary<string, ConnectionMetrics> _connectionMetrics = new();
        private readonly object _metricsLock = new();

        public RealtimeProxyService(
            IRealtimeMessageTranslatorFactory translatorFactory,
            IVirtualKeyService virtualKeyService,
            IRealtimeConnectionManager connectionManager,
            IRealtimeUsageTracker usageTracker,
            ILogger<RealtimeProxyService> logger)
        {
            _translatorFactory = translatorFactory ?? throw new ArgumentNullException(nameof(translatorFactory));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _usageTracker = usageTracker ?? throw new ArgumentNullException(nameof(usageTracker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleConnectionAsync(
            string connectionId,
            WebSocket clientWebSocket,
            VirtualKey virtualKey,
            string model,
            string? provider,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting proxy for connection {ConnectionId}, model {Model}, provider {Provider}",
                connectionId,
                model.Replace(Environment.NewLine, ""),
                provider?.Replace(Environment.NewLine, "") ?? "default");

            // Initialize connection metrics
            lock (_metricsLock)
            {
                _connectionMetrics[connectionId] = new ConnectionMetrics();
            }

            // Validate virtual key is enabled
            if (!virtualKey.IsEnabled)
            {
                throw new UnauthorizedAccessException("Virtual key is not active");
            }
            
            // Note: Budget validation happens at the service layer during key validation

            // Get the provider client and establish connection
            var audioRouter = _connectionManager as IAudioRouter ??
                throw new InvalidOperationException("Connection manager must implement IAudioRouter");

            // Create session configuration
            var sessionConfig = new RealtimeSessionConfig
            {
                Model = model,
                Voice = "alloy", // Default voice, could be made configurable
                SystemPrompt = "You are a helpful assistant."
            };

            var realtimeClient = await audioRouter.GetRealtimeClientAsync(sessionConfig, virtualKey.KeyHash);
            if (realtimeClient == null)
            {
                throw new InvalidOperationException($"No real-time audio provider available for model {model}");
            }

            // Connect to provider
            RealtimeSession? providerSession = null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Start usage tracking
                await _usageTracker.StartTrackingAsync(connectionId, virtualKey.Id, model, provider ?? "default");

                // Create the session
                providerSession = await realtimeClient.CreateSessionAsync(sessionConfig, virtualKey.KeyHash, cancellationToken);

                // Get the duplex stream from the provider
                var providerStream = realtimeClient.StreamAudioAsync(providerSession, cts.Token);

                // Start proxying in both directions
                var clientToProvider = ProxyClientToProviderAsync(
                    clientWebSocket, providerStream, connectionId, virtualKey.KeyHash, cts.Token);
                var providerToClient = ProxyProviderToClientAsync(
                    providerStream, clientWebSocket, connectionId, virtualKey.KeyHash, cts.Token);

                await Task.WhenAny(clientToProvider, providerToClient);

                // If one direction fails, cancel the other
                cts.Cancel();

                await Task.WhenAll(clientToProvider, providerToClient);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Proxy connection {ConnectionId} cancelled",
                connectionId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error in proxy for connection {ConnectionId}",
                connectionId);
                throw;
            }
            finally
            {
                try
                {
                    // Finalize usage tracking and update virtual key spend
                    var connectionInfo = await _connectionManager.GetConnectionAsync(connectionId);
                    var finalStats = connectionInfo?.Usage ?? new ConnectionUsageStats();
                    var totalCost = await _usageTracker.FinalizeUsageAsync(connectionId, finalStats);
                    
                    _logger.LogInformation("Session {ConnectionId} completed with total cost: ${Cost:F4}",
                connectionId,
                totalCost);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                "Error finalizing usage for connection {ConnectionId}",
                connectionId);
                }

                // Ensure client WebSocket is closed
                await CloseWebSocketAsync(clientWebSocket, "Proxy connection ended");

                // Close provider session
                if (providerSession != null)
                {
                    await realtimeClient.CloseSessionAsync(providerSession, cancellationToken);
                }
            }
        }








    }
}
