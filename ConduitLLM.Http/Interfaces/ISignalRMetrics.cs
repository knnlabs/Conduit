using System;
using System.Diagnostics.Metrics;

using ConduitLLM.Http.Interfaces;
namespace ConduitLLM.Http.Interfaces
{
    /// <summary>
    /// Interface for SignalR metrics to enable testing.
    /// </summary>
    public interface ISignalRMetrics : IDisposable
    {
        // Connection metrics
        Counter<long> ConnectionsTotal { get; }
        UpDownCounter<long> ActiveConnections { get; }
        Counter<long> AuthenticationFailures { get; }
        Counter<long> ConnectionErrors { get; }
        
        // Message metrics
        Counter<long> MessagesSent { get; }
        Counter<long> MessagesReceived { get; }
        Histogram<double> MessageProcessingDuration { get; }
        Counter<long> MessageErrors { get; }
        
        // Hub operation metrics
        Counter<long> HubMethodInvocations { get; }
        Histogram<double> HubMethodDuration { get; }
        Counter<long> HubErrors { get; }
        
        // Reconnection metrics
        Counter<long> ReconnectionAttempts { get; }
        Counter<long> ReconnectionSuccesses { get; }
        Counter<long> ReconnectionFailures { get; }
        
        // Group management metrics
        Counter<long> GroupJoins { get; }
        Counter<long> GroupLeaves { get; }
        UpDownCounter<long> ActiveGroups { get; }

        /// <summary>
        /// Records a hub method invocation with timing.
        /// </summary>
        IDisposable RecordHubMethodInvocation(string hubName, string methodName, int? virtualKeyId = null);

        /// <summary>
        /// Records a message processing operation with timing.
        /// </summary>
        IDisposable RecordMessageProcessing(string messageType, string direction);
    }
}