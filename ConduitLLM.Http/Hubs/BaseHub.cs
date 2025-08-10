using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Metrics;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Base class for all SignalR hubs that provides common functionality.
    /// This base class is for hubs that do not require authentication.
    /// </summary>
    public abstract class BaseHub : Hub
    {
        protected readonly ILogger Logger;
        private ISignalRMetrics? _metrics;

        protected BaseHub(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the SignalR metrics instance, lazily initialized from DI.
        /// </summary>
        protected ISignalRMetrics? Metrics
        {
            get
            {
                if (_metrics == null && Context.GetHttpContext() != null)
                {
                    _metrics = Context.GetHttpContext()!.RequestServices.GetService<ISignalRMetrics>();
                }
                return _metrics;
            }
        }

        public override async Task OnConnectedAsync()
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = Context.ConnectionId,
                ["HubName"] = GetHubName(),
                ["CorrelationId"] = correlationId
            }))
            {
                Logger.LogInformation("Client connected to {HubName}: {ConnectionId}", 
                    GetHubName(), Context.ConnectionId);
                
                await OnClientConnectedAsync();
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var correlationId = GetOrCreateCorrelationId();
            
            using (Logger.BeginScope(new Dictionary<string, object>
            {
                ["ConnectionId"] = Context.ConnectionId,
                ["HubName"] = GetHubName(),
                ["CorrelationId"] = correlationId
            }))
            {
                if (exception != null)
                {
                    Logger.LogWarning(exception, "Client disconnected from {HubName} with error: {ConnectionId}", 
                        GetHubName(), Context.ConnectionId);
                }
                else
                {
                    Logger.LogInformation("Client disconnected from {HubName}: {ConnectionId}", 
                        GetHubName(), Context.ConnectionId);
                }
                
                await OnClientDisconnectedAsync(exception);
                await base.OnDisconnectedAsync(exception);
            }
        }

        /// <summary>
        /// Called when a client successfully connects. Override to implement hub-specific logic.
        /// </summary>
        protected virtual Task OnClientConnectedAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a client disconnects. Override to implement hub-specific cleanup.
        /// </summary>
        protected virtual Task OnClientDisconnectedAsync(Exception? exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the name of the hub for logging purposes
        /// </summary>
        protected abstract string GetHubName();

        /// <summary>
        /// Adds the current connection to a named group.
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        protected async Task AddToGroupAsync(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            
            Logger.LogDebug("Added connection {ConnectionId} to group {GroupName} in {HubName}", 
                Context.ConnectionId, groupName, GetHubName());
            
            Metrics?.GroupJoins.Add(1, new TagList 
            { 
                { "hub", GetHubName() }, 
                { "group", groupName } 
            });
        }

        /// <summary>
        /// Removes the current connection from a named group.
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        protected async Task RemoveFromGroupAsync(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            
            Logger.LogDebug("Removed connection {ConnectionId} from group {GroupName} in {HubName}", 
                Context.ConnectionId, groupName, GetHubName());
            
            Metrics?.GroupLeaves.Add(1, new TagList 
            { 
                { "hub", GetHubName() }, 
                { "group", groupName } 
            });
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