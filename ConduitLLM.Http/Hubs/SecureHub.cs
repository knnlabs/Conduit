using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Metrics;
using ConduitLLM.Http.Authentication;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Base class for all secure SignalR hubs that require virtual key authentication.
    /// Provides common functionality for connection management, authentication, and virtual key extraction.
    /// </summary>
    [VirtualKeyHubAuthorization]
    public abstract class SecureHub : Hub
    {
        protected readonly ILogger Logger;
        private readonly IServiceProvider _serviceProvider;
        private ISignalRAuthenticationService? _authService;

        protected SecureHub(ILogger logger, IServiceProvider serviceProvider)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }
        
        /// <summary>
        /// Gets the authentication service, lazily initialized from DI.
        /// </summary>
        protected ISignalRAuthenticationService AuthService => 
            _authService ??= _serviceProvider.GetRequiredService<ISignalRAuthenticationService>();

        public override async Task OnConnectedAsync()
        {
            var virtualKeyId = GetVirtualKeyId();
            var virtualKeyName = GetVirtualKeyName();
            var correlationId = GetOrCreateCorrelationId();
            
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = Context.ConnectionId,
                ["HubName"] = GetHubName(),
                ["VirtualKeyId"] = virtualKeyId?.ToString() ?? "anonymous",
                ["VirtualKeyName"] = virtualKeyName,
                ["CorrelationId"] = correlationId
            }))
            {
                if (!virtualKeyId.HasValue)
                {
                    Logger.LogWarning("Connection without valid virtual key ID to {HubName}", GetHubName());
                    
                    var metrics = GetMetrics();
                    metrics?.AuthenticationFailures.Add(1, new TagList { { "hub", GetHubName() } });
                    
                    Context.Abort();
                    return;
                }
                
                // Add to virtual-key-specific group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
                
                Logger.LogInformation("Virtual Key {KeyName} (ID: {KeyId}) connected to {HubName}: {ConnectionId}", 
                    virtualKeyName, virtualKeyId, GetHubName(), Context.ConnectionId);
                
                await OnVirtualKeyConnectedAsync(virtualKeyId.Value, virtualKeyName);
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var virtualKeyId = GetVirtualKeyId();
            var virtualKeyName = GetVirtualKeyName();
            var correlationId = GetOrCreateCorrelationId();
            
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = Context.ConnectionId,
                ["HubName"] = GetHubName(),
                ["VirtualKeyId"] = virtualKeyId?.ToString() ?? "anonymous",
                ["VirtualKeyName"] = virtualKeyName,
                ["CorrelationId"] = correlationId
            }))
            {
                Logger.LogInformation("Virtual Key {KeyName} disconnected from {HubName}: {ConnectionId}", 
                    virtualKeyName, GetHubName(), Context.ConnectionId);
                
                if (virtualKeyId.HasValue)
                {
                    // Remove from virtual-key-specific group
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
                    
                    await OnVirtualKeyDisconnectedAsync(virtualKeyId.Value, virtualKeyName, exception);
                }
                
                await base.OnDisconnectedAsync(exception);
            }
        }

        /// <summary>
        /// Called when a virtual key successfully connects. Override to implement hub-specific logic.
        /// </summary>
        protected virtual Task OnVirtualKeyConnectedAsync(int virtualKeyId, string virtualKeyName)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a virtual key disconnects. Override to implement hub-specific cleanup.
        /// </summary>
        protected virtual Task OnVirtualKeyDisconnectedAsync(int virtualKeyId, string virtualKeyName, Exception? exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the virtual key ID from the connection context
        /// </summary>
        protected int? GetVirtualKeyId()
        {
            return AuthService.GetVirtualKeyId(Context);
        }
        
        /// <summary>
        /// Gets the virtual key name from the connection context
        /// </summary>
        protected string GetVirtualKeyName()
        {
            return AuthService.GetVirtualKeyName(Context);
        }

        /// <summary>
        /// Ensures the current connection has a valid virtual key ID, throwing a HubException if not
        /// </summary>
        protected int RequireVirtualKeyId()
        {
            var virtualKeyId = GetVirtualKeyId();
            if (!virtualKeyId.HasValue)
            {
                throw new HubException("Unauthorized");
            }
            return virtualKeyId.Value;
        }


        /// <summary>
        /// Gets the name of the hub for logging purposes
        /// </summary>
        protected abstract string GetHubName();

        /// <summary>
        /// Verifies if the current virtual key can access a specific task.
        /// </summary>
        /// <param name="taskId">The task ID to verify access for</param>
        /// <returns>True if the virtual key owns the task, false otherwise</returns>
        protected async Task<bool> CanAccessTaskAsync(string taskId)
        {
            return await AuthService.CanAccessResourceAsync(Context, "task", taskId);
        }
        
        /// <summary>
        /// Gets the authenticated virtual key entity.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to retrieve</param>
        /// <returns>The virtual key entity if found and enabled, null otherwise</returns>
        protected async Task<VirtualKey?> GetVirtualKeyAsync(int virtualKeyId)
        {
            return await AuthService.GetAuthenticatedVirtualKeyAsync(Context);
        }
        
        /// <summary>
        /// Checks if the current virtual key has admin privileges.
        /// </summary>
        /// <returns>True if the virtual key is an admin</returns>
        protected async Task<bool> IsAdminAsync()
        {
            return await AuthService.IsAdminAsync(Context);
        }

        /// <summary>
        /// Adds the current connection to the virtual key's group for receiving broadcasts.
        /// </summary>
        protected async Task AddToVirtualKeyGroupAsync()
        {
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
                Logger.LogDebug("Added connection {ConnectionId} to virtual key group {VirtualKeyId}", 
                    Context.ConnectionId, virtualKeyId.Value);
            }
        }

        /// <summary>
        /// Gets the virtual key ID from the connection context, using the property syntax requested.
        /// </summary>
        protected string? VirtualKeyId => GetVirtualKeyId()?.ToString();

        /// <summary>
        /// Gets the SignalR metrics instance from the service provider.
        /// </summary>
        private ISignalRMetrics? GetMetrics()
        {
            return _serviceProvider.GetService<ISignalRMetrics>();
        }

        /// <summary>
        /// Gets or creates a correlation ID for the current connection.
        /// </summary>
        protected string GetOrCreateCorrelationId()
        {
            if (Context.Items.TryGetValue("CorrelationId", out var value) && value is string correlationId)
            {
                return correlationId;
            }

            correlationId = Guid.NewGuid().ToString();
            Context.Items["CorrelationId"] = correlationId;
            return correlationId;
        }
    }
}