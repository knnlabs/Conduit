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

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Base class for all secure SignalR hubs that require virtual key authentication.
    /// Provides common functionality for connection management, authentication, and virtual key extraction.
    /// </summary>
    [Authorize]
    public abstract class SecureHub : Hub
    {
        protected readonly ILogger Logger;
        private readonly IServiceProvider _serviceProvider;

        protected SecureHub(ILogger logger, IServiceProvider serviceProvider)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

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
            // Try from Items first (set by hub filter)
            if (Context.Items.TryGetValue("VirtualKeyId", out var itemValue) && itemValue is int itemId)
            {
                return itemId;
            }
            
            // Try from User claims (set by authentication handler)
            var claim = Context.User?.FindFirst("VirtualKeyId");
            if (claim != null && int.TryParse(claim.Value, out var claimId))
            {
                return claimId;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the virtual key name from the connection context
        /// </summary>
        protected string GetVirtualKeyName()
        {
            // Try from Items first (set by hub filter)
            if (Context.Items.TryGetValue("VirtualKeyName", out var itemValue) && itemValue is string itemName)
            {
                return itemName;
            }
            
            // Try from User claims (set by authentication handler)
            return Context.User?.Identity?.Name ?? "Unknown";
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
        /// Converts various object types to int, useful for parsing metadata
        /// </summary>
        protected static int? ConvertToInt(object value)
        {
            if (value is int intValue)
                return intValue;
            
            if (value is long longValue)
                return (int)longValue;
            
            if (value is string stringValue && int.TryParse(stringValue, out var parsedValue))
                return parsedValue;
            
            return null;
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
            var virtualKeyId = GetVirtualKeyId();
            if (!virtualKeyId.HasValue)
            {
                Logger.LogWarning("Cannot verify task access without virtual key ID");
                return false;
            }

            try
            {
                var taskService = _serviceProvider.GetService<IAsyncTaskService>();
                if (taskService == null)
                {
                    Logger.LogWarning("IAsyncTaskService not available, cannot verify task ownership");
                    return false;
                }

                var taskStatus = await taskService.GetTaskStatusAsync(taskId);
                if (taskStatus == null)
                {
                    Logger.LogWarning("Task {TaskId} not found", taskId);
                    return false;
                }

                // Check if the task metadata contains the virtual key ID
                if (taskStatus.Metadata is IDictionary<string, object> metadata)
                {
                    if (metadata.TryGetValue("virtualKeyId", out var taskVirtualKeyIdObj))
                    {
                        var taskVirtualKeyId = ConvertToInt(taskVirtualKeyIdObj);
                        if (taskVirtualKeyId.HasValue && taskVirtualKeyId.Value == virtualKeyId.Value)
                        {
                            Logger.LogDebug("Virtual Key {VirtualKeyId} has access to task {TaskId}", 
                                virtualKeyId.Value, taskId);
                            return true;
                        }
                        else
                        {
                            Logger.LogWarning("Virtual Key {VirtualKeyId} does not have access to task {TaskId} owned by Virtual Key {OwnerKeyId}", 
                                virtualKeyId.Value, taskId, taskVirtualKeyId);
                            return false;
                        }
                    }
                }

                Logger.LogWarning("Task {TaskId} has no virtual key metadata", taskId);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking task access for {TaskId}", taskId);
                return false;
            }
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
        private SignalRMetrics? GetMetrics()
        {
            return _serviceProvider.GetService<SignalRMetrics>();
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