using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.WebUI.Interfaces;
using System.Collections.Generic;
using System.Text.Json;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Server-side SignalR client that connects to the Core API hubs
    /// and relays events to Blazor components
    /// </summary>
    public class ServerSideSignalRService : IHostedService, IDisposable
    {
        private readonly ILogger<ServerSideSignalRService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, HubConnection> _hubConnections;
        private readonly List<IServerSideSignalRListener> _listeners;
        private readonly SemaphoreSlim _connectionLock;
        private string? _virtualKey;
        private Timer? _reconnectTimer;
        private bool _isDisposing;

        public ServerSideSignalRService(
            ILogger<ServerSideSignalRService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _hubConnections = new Dictionary<string, HubConnection>();
            _listeners = new List<IServerSideSignalRListener>();
            _connectionLock = new SemaphoreSlim(1, 1);
        }

        public ConnectionState GetConnectionState(string hubName)
        {
            if (_hubConnections.TryGetValue(hubName, out var connection))
            {
                return connection.State switch
                {
                    HubConnectionState.Connected => ConnectionState.Connected,
                    HubConnectionState.Connecting => ConnectionState.Connecting,
                    HubConnectionState.Reconnecting => ConnectionState.Reconnecting,
                    _ => ConnectionState.Disconnected
                };
            }
            return ConnectionState.Disconnected;
        }

        public void RegisterListener(IServerSideSignalRListener listener)
        {
            lock (_listeners)
            {
                if (!_listeners.Contains(listener))
                {
                    _listeners.Add(listener);
                    _logger.LogDebug("Registered SignalR listener: {ListenerType}", listener.GetType().Name);
                }
            }
        }

        public void UnregisterListener(IServerSideSignalRListener listener)
        {
            lock (_listeners)
            {
                _listeners.Remove(listener);
                _logger.LogDebug("Unregistered SignalR listener: {ListenerType}", listener.GetType().Name);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting server-side SignalR service");

            try
            {
                // Get the virtual key from global settings
                using (var scope = _serviceProvider.CreateScope())
                {
                    var globalSettingService = scope.ServiceProvider.GetRequiredService<IGlobalSettingService>();
                    _virtualKey = await globalSettingService.GetSettingAsync("WebUI_VirtualKey");
                    
                    if (string.IsNullOrEmpty(_virtualKey))
                    {
                        _logger.LogWarning("WebUI virtual key not found in settings");
                        return;
                    }
                }

                // Initialize hub connections
                await InitializeHubConnection("notifications", "/hubs/notifications");
                await InitializeHubConnection("video-generation", "/hubs/video-generation");
                await InitializeHubConnection("image-generation", "/hubs/image-generation");

                _logger.LogInformation("Server-side SignalR service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start server-side SignalR service");
                // Schedule reconnection attempt
                ScheduleReconnection();
            }
        }

        private async Task InitializeHubConnection(string hubName, string hubPath)
        {
            try
            {
                var apiBaseUrl = _configuration["CONDUIT_API_BASE_URL"] ?? "http://api:8080";
                var hubUrl = $"{apiBaseUrl}{hubPath}";

                _logger.LogInformation("Connecting to {HubName} hub at {HubUrl}", hubName, hubUrl);

                var connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_virtualKey);
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                    .Build();

                // Set up event handlers based on hub type
                switch (hubName)
                {
                    case "notifications":
                        SetupNotificationHandlers(connection);
                        break;
                    case "video-generation":
                        SetupVideoGenerationHandlers(connection);
                        break;
                    case "image-generation":
                        SetupImageGenerationHandlers(connection);
                        break;
                }

                // Connection lifecycle events
                connection.Closed += async (error) =>
                {
                    _logger.LogWarning(error, "Connection to {HubName} hub closed", hubName);
                    await NotifyConnectionStateChanged(hubName, ConnectionState.Disconnected);
                    
                    if (!_isDisposing)
                    {
                        ScheduleReconnection();
                    }
                };

                connection.Reconnecting += async (error) =>
                {
                    _logger.LogInformation("Reconnecting to {HubName} hub", hubName);
                    await NotifyConnectionStateChanged(hubName, ConnectionState.Reconnecting);
                };

                connection.Reconnected += async (connectionId) =>
                {
                    _logger.LogInformation("Reconnected to {HubName} hub with ID: {ConnectionId}", hubName, connectionId);
                    await NotifyConnectionStateChanged(hubName, ConnectionState.Connected);
                };

                // Start the connection
                await connection.StartAsync();
                _hubConnections[hubName] = connection;
                
                _logger.LogInformation("Connected to {HubName} hub successfully", hubName);
                await NotifyConnectionStateChanged(hubName, ConnectionState.Connected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to {HubName} hub", hubName);
                await NotifyConnectionStateChanged(hubName, ConnectionState.Disconnected);
                throw;
            }
        }

        private void SetupNotificationHandlers(HubConnection connection)
        {
            connection.On<JsonElement>("ReceiveSystemNotification", async (notification) =>
            {
                _logger.LogDebug("Received system notification");
                await NotifyListeners(listener => listener.OnSystemNotificationReceived(notification));
            });

            connection.On<JsonElement>("ModelMappingChanged", async (data) =>
            {
                _logger.LogDebug("Received model mapping change");
                await NotifyListeners(listener => listener.OnModelMappingChanged(data));
            });

            connection.On<JsonElement>("ProviderHealthChanged", async (data) =>
            {
                _logger.LogDebug("Received provider health change");
                await NotifyListeners(listener => listener.OnProviderHealthChanged(data));
            });

            connection.On<JsonElement>("NavigationStateChanged", async (data) =>
            {
                _logger.LogDebug("Received navigation state change");
                await NotifyListeners(listener => listener.OnNavigationStateChanged(data));
            });
            
            // Spend notification events
            connection.On<JsonElement>("SpendUpdate", async (notification) =>
            {
                _logger.LogDebug("Received spend update notification");
                await NotifyListeners(listener => listener.OnSpendUpdate(notification));
            });
            
            connection.On<JsonElement>("BudgetAlert", async (notification) =>
            {
                _logger.LogDebug("Received budget alert notification");
                await NotifyListeners(listener => listener.OnBudgetAlert(notification));
            });
            
            connection.On<JsonElement>("SpendSummary", async (notification) =>
            {
                _logger.LogDebug("Received spend summary notification");
                await NotifyListeners(listener => listener.OnSpendSummary(notification));
            });
            
            connection.On<JsonElement>("UnusualSpending", async (notification) =>
            {
                _logger.LogDebug("Received unusual spending notification");
                await NotifyListeners(listener => listener.OnUnusualSpending(notification));
            });
            
            // Model discovery events
            connection.On<JsonElement>("NewModelsDiscovered", async (notification) =>
            {
                _logger.LogDebug("Received new models discovered notification");
                await NotifyListeners(listener => listener.OnNewModelsDiscovered(notification));
            });
            
            connection.On<JsonElement>("ModelCapabilitiesChanged", async (notification) =>
            {
                _logger.LogDebug("Received model capabilities changed notification");
                await NotifyListeners(listener => listener.OnModelCapabilitiesChanged(notification));
            });
            
            connection.On<JsonElement>("ModelPricingUpdated", async (notification) =>
            {
                _logger.LogDebug("Received model pricing updated notification");
                await NotifyListeners(listener => listener.OnModelPricingUpdated(notification));
            });
            
            connection.On<JsonElement>("ModelDeprecated", async (notification) =>
            {
                _logger.LogDebug("Received model deprecated notification");
                await NotifyListeners(listener => listener.OnModelDeprecated(notification));
            });
            
            // Batch operation events
            connection.On<string, JsonElement>("BatchOperationProgress", async (operationId, progress) =>
            {
                _logger.LogDebug("Received batch operation progress for {OperationId}", operationId);
                await NotifyListeners(listener => listener.OnBatchOperationProgress(operationId, progress));
            });
            
            connection.On<string, JsonElement>("BatchOperationCompleted", async (operationId, result) =>
            {
                _logger.LogDebug("Batch operation completed for {OperationId}", operationId);
                await NotifyListeners(listener => listener.OnBatchOperationCompleted(operationId, result));
            });
            
            connection.On<string, string>("BatchOperationFailed", async (operationId, error) =>
            {
                _logger.LogDebug("Batch operation failed for {OperationId}", operationId);
                await NotifyListeners(listener => listener.OnBatchOperationFailed(operationId, error));
            });
            
            // Admin notification events
            connection.On<JsonElement>("AdminNotification", async (notification) =>
            {
                _logger.LogDebug("Received admin notification");
                await NotifyListeners(listener => listener.OnAdminNotificationReceived(notification));
            });
        }

        private void SetupVideoGenerationHandlers(HubConnection connection)
        {
            connection.On<string, JsonElement>("VideoGenerationProgress", async (taskId, progress) =>
            {
                _logger.LogDebug("Received video generation progress for task {TaskId}", taskId);
                await NotifyListeners(listener => listener.OnVideoGenerationProgress(taskId, progress));
            });

            connection.On<string, JsonElement>("VideoGenerationCompleted", async (taskId, result) =>
            {
                _logger.LogDebug("Video generation completed for task {TaskId}", taskId);
                await NotifyListeners(listener => listener.OnVideoGenerationCompleted(taskId, result));
            });

            connection.On<string, string>("VideoGenerationFailed", async (taskId, error) =>
            {
                _logger.LogDebug("Video generation failed for task {TaskId}", taskId);
                await NotifyListeners(listener => listener.OnVideoGenerationFailed(taskId, error));
            });
        }

        private void SetupImageGenerationHandlers(HubConnection connection)
        {
            connection.On<string, JsonElement>("ImageGenerationProgress", async (taskId, progress) =>
            {
                _logger.LogDebug("Received image generation progress for task {TaskId}", taskId);
                await NotifyListeners(listener => listener.OnImageGenerationProgress(taskId, progress));
            });

            connection.On<string, JsonElement>("ImageGenerationCompleted", async (taskId, result) =>
            {
                _logger.LogDebug("Image generation completed for task {TaskId}", taskId);
                await NotifyListeners(listener => listener.OnImageGenerationCompleted(taskId, result));
            });

            connection.On<string, string>("ImageGenerationFailed", async (taskId, error) =>
            {
                _logger.LogDebug("Image generation failed for task {TaskId}", taskId);
                await NotifyListeners(listener => listener.OnImageGenerationFailed(taskId, error));
            });
        }

        private async Task NotifyListeners(Func<IServerSideSignalRListener, Task> action)
        {
            var listeners = new List<IServerSideSignalRListener>();
            lock (_listeners)
            {
                listeners.AddRange(_listeners);
            }

            var tasks = listeners.Select(listener => SafeNotify(listener, action));
            await Task.WhenAll(tasks);
        }

        private async Task SafeNotify(IServerSideSignalRListener listener, Func<IServerSideSignalRListener, Task> action)
        {
            try
            {
                await action(listener);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying listener {ListenerType}", listener.GetType().Name);
            }
        }

        private async Task NotifyConnectionStateChanged(string hubName, ConnectionState state)
        {
            await NotifyListeners(listener => listener.OnConnectionStateChanged(hubName, state));
        }

        /// <summary>
        /// Subscribe to updates for a specific video generation task
        /// </summary>
        public async Task SubscribeToVideoTask(string taskId)
        {
            if (_hubConnections.TryGetValue("video-generation", out var connection) && 
                connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await connection.InvokeAsync("SubscribeToTask", taskId);
                    _logger.LogInformation("Subscribed to video generation task {TaskId}", taskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe to video task {TaskId}", taskId);
                }
            }
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation task
        /// </summary>
        public async Task UnsubscribeFromVideoTask(string taskId)
        {
            if (_hubConnections.TryGetValue("video-generation", out var connection) && 
                connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await connection.InvokeAsync("UnsubscribeFromTask", taskId);
                    _logger.LogInformation("Unsubscribed from video generation task {TaskId}", taskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unsubscribe from video task {TaskId}", taskId);
                }
            }
        }

        /// <summary>
        /// Subscribe to updates for a specific image generation task
        /// </summary>
        public async Task SubscribeToImageTask(string taskId)
        {
            if (_hubConnections.TryGetValue("image-generation", out var connection) && 
                connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await connection.InvokeAsync("SubscribeToTask", taskId);
                    _logger.LogInformation("Subscribed to image generation task {TaskId}", taskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to subscribe to image task {TaskId}", taskId);
                }
            }
        }

        /// <summary>
        /// Unsubscribe from updates for a specific image generation task
        /// </summary>
        public async Task UnsubscribeFromImageTask(string taskId)
        {
            if (_hubConnections.TryGetValue("image-generation", out var connection) && 
                connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await connection.InvokeAsync("UnsubscribeFromTask", taskId);
                    _logger.LogInformation("Unsubscribed from image generation task {TaskId}", taskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unsubscribe from image task {TaskId}", taskId);
                }
            }
        }

        private void ScheduleReconnection()
        {
            if (_isDisposing) return;

            _reconnectTimer?.Dispose();
            _reconnectTimer = new Timer(async _ =>
            {
                if (_isDisposing) return;

                _logger.LogInformation("Attempting to reconnect SignalR connections");
                try
                {
                    await StartAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconnection attempt failed");
                    ScheduleReconnection();
                }
            }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping server-side SignalR service");
            _isDisposing = true;

            _reconnectTimer?.Dispose();

            foreach (var kvp in _hubConnections)
            {
                try
                {
                    await kvp.Value.DisposeAsync();
                    _logger.LogInformation("Disconnected from {HubName} hub", kvp.Key);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disconnecting from {HubName} hub", kvp.Key);
                }
            }

            _hubConnections.Clear();
        }

        public void Dispose()
        {
            _isDisposing = true;
            _reconnectTimer?.Dispose();
            _connectionLock?.Dispose();
            
            foreach (var connection in _hubConnections.Values)
            {
                connection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
        }
    }
}