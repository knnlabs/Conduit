using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConduitLLM.Http.Metrics
{
    /// <summary>
    /// Provides OpenTelemetry metrics for SignalR operations.
    /// </summary>
    public class SignalRMetrics : ISignalRMetrics
    {
        private readonly Meter _meter;
        
        // Connection metrics
        public Counter<long> ConnectionsTotal { get; }
        public UpDownCounter<long> ActiveConnections { get; }
        public Counter<long> AuthenticationFailures { get; }
        public Counter<long> ConnectionErrors { get; }
        
        // Message metrics
        public Counter<long> MessagesSent { get; }
        public Counter<long> MessagesReceived { get; }
        public Histogram<double> MessageProcessingDuration { get; }
        public Counter<long> MessageErrors { get; }
        
        // Hub operation metrics
        public Counter<long> HubMethodInvocations { get; }
        public Histogram<double> HubMethodDuration { get; }
        public Counter<long> HubErrors { get; }
        
        // Reconnection metrics
        public Counter<long> ReconnectionAttempts { get; }
        public Counter<long> ReconnectionSuccesses { get; }
        public Counter<long> ReconnectionFailures { get; }
        
        // Group management metrics
        public Counter<long> GroupJoins { get; }
        public Counter<long> GroupLeaves { get; }
        public UpDownCounter<long> ActiveGroups { get; }

        public SignalRMetrics(string meterName = "ConduitLLM.SignalR")
        {
            _meter = new Meter(meterName, "1.0.0");
            
            // Initialize connection metrics
            ConnectionsTotal = _meter.CreateCounter<long>(
                "signalr_connections_total",
                description: "Total number of SignalR connections established");
                
            ActiveConnections = _meter.CreateUpDownCounter<long>(
                "signalr_connections_active",
                description: "Number of currently active SignalR connections");
                
            AuthenticationFailures = _meter.CreateCounter<long>(
                "signalr_authentication_failures_total",
                description: "Total number of SignalR authentication failures");
                
            ConnectionErrors = _meter.CreateCounter<long>(
                "signalr_connection_errors_total",
                description: "Total number of SignalR connection errors");
            
            // Initialize message metrics
            MessagesSent = _meter.CreateCounter<long>(
                "signalr_messages_sent_total",
                description: "Total number of messages sent via SignalR");
                
            MessagesReceived = _meter.CreateCounter<long>(
                "signalr_messages_received_total",
                description: "Total number of messages received via SignalR");
                
            MessageProcessingDuration = _meter.CreateHistogram<double>(
                "signalr_message_processing_duration_seconds",
                unit: "s",
                description: "Duration of SignalR message processing in seconds");
                
            MessageErrors = _meter.CreateCounter<long>(
                "signalr_message_errors_total",
                description: "Total number of SignalR message processing errors");
            
            // Initialize hub operation metrics
            HubMethodInvocations = _meter.CreateCounter<long>(
                "signalr_hub_method_invocations_total",
                description: "Total number of SignalR hub method invocations");
                
            HubMethodDuration = _meter.CreateHistogram<double>(
                "signalr_hub_method_duration_seconds",
                unit: "s",
                description: "Duration of SignalR hub method execution in seconds");
                
            HubErrors = _meter.CreateCounter<long>(
                "signalr_hub_errors_total",
                description: "Total number of SignalR hub errors");
            
            // Initialize reconnection metrics
            ReconnectionAttempts = _meter.CreateCounter<long>(
                "signalr_reconnection_attempts_total",
                description: "Total number of SignalR reconnection attempts");
                
            ReconnectionSuccesses = _meter.CreateCounter<long>(
                "signalr_reconnection_successes_total",
                description: "Total number of successful SignalR reconnections");
                
            ReconnectionFailures = _meter.CreateCounter<long>(
                "signalr_reconnection_failures_total",
                description: "Total number of failed SignalR reconnections");
            
            // Initialize group management metrics
            GroupJoins = _meter.CreateCounter<long>(
                "signalr_group_joins_total",
                description: "Total number of SignalR group joins");
                
            GroupLeaves = _meter.CreateCounter<long>(
                "signalr_group_leaves_total",
                description: "Total number of SignalR group leaves");
                
            ActiveGroups = _meter.CreateUpDownCounter<long>(
                "signalr_groups_active",
                description: "Number of currently active SignalR groups");
        }

        /// <summary>
        /// Records a hub method invocation with timing.
        /// </summary>
        public IDisposable RecordHubMethodInvocation(string hubName, string methodName, int? virtualKeyId = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var tags = new TagList
            {
                { "hub", hubName },
                { "method", methodName }
            };
            
            if (virtualKeyId.HasValue)
            {
                tags.Add("virtual_key_id", virtualKeyId.Value.ToString());
            }
            
            HubMethodInvocations.Add(1, tags);
            
            return new HubMethodTimer(this, stopwatch, tags);
        }

        /// <summary>
        /// Records a message processing operation with timing.
        /// </summary>
        public IDisposable RecordMessageProcessing(string messageType, string direction)
        {
            var stopwatch = Stopwatch.StartNew();
            var tags = new TagList
            {
                { "message_type", messageType },
                { "direction", direction }
            };
            
            if (direction == "receive")
            {
                MessagesReceived.Add(1, tags);
            }
            else if (direction == "send")
            {
                MessagesSent.Add(1, tags);
            }
            
            return new MessageProcessingTimer(this, stopwatch, tags);
        }

        public void Dispose()
        {
            _meter?.Dispose();
        }

        private class HubMethodTimer : IDisposable
        {
            private readonly SignalRMetrics _metrics;
            private readonly Stopwatch _stopwatch;
            private readonly TagList _tags;
            private bool _disposed;

            public HubMethodTimer(SignalRMetrics metrics, Stopwatch stopwatch, TagList tags)
            {
                _metrics = metrics;
                _stopwatch = stopwatch;
                _tags = tags;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _stopwatch.Stop();
                    _metrics.HubMethodDuration.Record(_stopwatch.Elapsed.TotalSeconds, _tags);
                    _disposed = true;
                }
            }
        }

        private class MessageProcessingTimer : IDisposable
        {
            private readonly SignalRMetrics _metrics;
            private readonly Stopwatch _stopwatch;
            private readonly TagList _tags;
            private bool _disposed;

            public MessageProcessingTimer(SignalRMetrics metrics, Stopwatch stopwatch, TagList tags)
            {
                _metrics = metrics;
                _stopwatch = stopwatch;
                _tags = tags;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _stopwatch.Stop();
                    _metrics.MessageProcessingDuration.Record(_stopwatch.Elapsed.TotalSeconds, _tags);
                    _disposed = true;
                }
            }
        }
    }
}