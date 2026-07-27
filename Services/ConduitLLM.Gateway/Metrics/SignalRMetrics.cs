using System.Diagnostics;
using System.Diagnostics.Metrics;

using ConduitLLM.Gateway.Interfaces;
namespace ConduitLLM.Gateway.Metrics
{
    /// <summary>
    /// OpenTelemetry metrics for SignalR operations
    /// </summary>
    public class SignalRMetrics : ISignalRMetrics
    {
        private readonly Meter _meter;

        // ISignalRMetrics properties - Connection metrics
        public Counter<long> ConnectionsTotal { get; }
        public UpDownCounter<long> ActiveConnections { get; }
        public Counter<long> AuthenticationFailures { get; }
        public Counter<long> ConnectionErrors { get; }

        // ISignalRMetrics properties - Message metrics
        public Counter<long> MessagesSent { get; }
        public Counter<long> MessagesReceived { get; }
        public Histogram<double> MessageProcessingDuration { get; }
        public Counter<long> MessageErrors { get; }

        // ISignalRMetrics properties - Hub operation metrics
        public Counter<long> HubMethodInvocations { get; }
        public Histogram<double> HubMethodDuration { get; }
        public Counter<long> HubErrors { get; }

        // ISignalRMetrics properties - Reconnection metrics
        public Counter<long> ReconnectionAttempts { get; }
        public Counter<long> ReconnectionSuccesses { get; }
        public Counter<long> ReconnectionFailures { get; }

        // ISignalRMetrics properties - Group management metrics
        public Counter<long> GroupJoins { get; }
        public Counter<long> GroupLeaves { get; }
        public UpDownCounter<long> ActiveGroups { get; }

        // Activity source for distributed tracing
        public static readonly ActivitySource ActivitySource = new("ConduitLLM.SignalR", "1.0.0");

        public SignalRMetrics(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create("ConduitLLM.SignalR", "1.0.0");

            // Initialize ISignalRMetrics required counters
            ConnectionsTotal = _meter.CreateCounter<long>(
                "signalr.connections.total",
                "connections",
                "Total number of connections created");

            ActiveConnections = _meter.CreateUpDownCounter<long>(
                "signalr.connections.active",
                "connections",
                "Number of active connections");

            AuthenticationFailures = _meter.CreateCounter<long>(
                "signalr.authentication.failures",
                "failures",
                "Number of authentication failures");

            ConnectionErrors = _meter.CreateCounter<long>(
                "signalr.connections.errors",
                "errors",
                "Number of connection errors");

            MessagesSent = _meter.CreateCounter<long>(
                "signalr.messages.sent",
                "messages",
                "Number of messages sent");

            MessagesReceived = _meter.CreateCounter<long>(
                "signalr.messages.received",
                "messages",
                "Number of messages received");

            MessageProcessingDuration = _meter.CreateHistogram<double>(
                "signalr.message.processing.duration",
                "milliseconds",
                "Duration of message processing");

            MessageErrors = _meter.CreateCounter<long>(
                "signalr.messages.errors",
                "errors",
                "Number of message errors");

            HubMethodInvocations = _meter.CreateCounter<long>(
                "signalr.hub.method.invocations",
                "invocations",
                "Number of hub method invocations");

            HubMethodDuration = _meter.CreateHistogram<double>(
                "signalr.hub.method.duration",
                "milliseconds",
                "Duration of hub method invocations");

            HubErrors = _meter.CreateCounter<long>(
                "signalr.hub.errors",
                "errors",
                "Number of hub errors");

            ReconnectionAttempts = _meter.CreateCounter<long>(
                "signalr.reconnection.attempts",
                "attempts",
                "Number of reconnection attempts");

            ReconnectionSuccesses = _meter.CreateCounter<long>(
                "signalr.reconnection.successes",
                "successes",
                "Number of successful reconnections");

            ReconnectionFailures = _meter.CreateCounter<long>(
                "signalr.reconnection.failures",
                "failures",
                "Number of failed reconnections");

            GroupJoins = _meter.CreateCounter<long>(
                "signalr.group.joins",
                "joins",
                "Number of group joins");

            GroupLeaves = _meter.CreateCounter<long>(
                "signalr.group.leaves",
                "leaves",
                "Number of group leaves");

            ActiveGroups = _meter.CreateUpDownCounter<long>(
                "signalr.groups.active",
                "groups",
                "Number of active groups");

        }

        /// <summary>
        /// Records a hub method invocation with timing.
        /// </summary>
        public IDisposable RecordHubMethodInvocation(string hubName, string methodName, int? virtualKeyId = null, string? protocol = null)
        {
            var tags = new TagList
            {
                { "hub", hubName },
                { "method", methodName }
            };

            if (virtualKeyId.HasValue)
            {
                tags.Add("virtual_key_id", virtualKeyId.Value.ToString());
            }

            if (!string.IsNullOrEmpty(protocol))
            {
                tags.Add("protocol", protocol);
            }

            HubMethodInvocations.Add(1, tags);

            var stopwatch = Stopwatch.StartNew();
            return new MethodTimer(this, stopwatch, hubName, methodName, protocol);
        }

        /// <summary>
        /// Records a message processing operation with timing.
        /// </summary>
        public IDisposable RecordMessageProcessing(string messageType, string direction, string? protocol = null)
        {
            var tags = new TagList
            {
                { "message_type", messageType },
                { "direction", direction }
            };

            if (!string.IsNullOrEmpty(protocol))
            {
                tags.Add("protocol", protocol);
            }

            if (direction == "sent")
                MessagesSent.Add(1, tags);
            else if (direction == "received")
                MessagesReceived.Add(1, tags);

            var stopwatch = Stopwatch.StartNew();
            return new MessageProcessingTimer(this, stopwatch, messageType, direction, protocol);
        }

        public static Activity? StartMessageActivity(string operationName, string hub, string method)
        {
            return ActivitySource.StartActivity(operationName, ActivityKind.Internal, Activity.Current?.Context ?? default, new TagList
            {
                { "signalr.hub", hub },
                { "signalr.method", method }
            });
        }

        public void Dispose()
        {
            _meter?.Dispose();
        }

        private class MethodTimer : IDisposable
        {
            private readonly SignalRMetrics _metrics;
            private readonly Stopwatch _stopwatch;
            private readonly string _hubName;
            private readonly string _methodName;
            private readonly string? _protocol;

            public MethodTimer(SignalRMetrics metrics, Stopwatch stopwatch, string hubName, string methodName, string? protocol = null)
            {
                _metrics = metrics;
                _stopwatch = stopwatch;
                _hubName = hubName;
                _methodName = methodName;
                _protocol = protocol;
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                var tags = new TagList
                {
                    { "hub", _hubName },
                    { "method", _methodName }
                };

                if (!string.IsNullOrEmpty(_protocol))
                {
                    tags.Add("protocol", _protocol);
                }

                _metrics.HubMethodDuration.Record(_stopwatch.ElapsedMilliseconds, tags);
            }
        }

        private class MessageProcessingTimer : IDisposable
        {
            private readonly SignalRMetrics _metrics;
            private readonly Stopwatch _stopwatch;
            private readonly string _messageType;
            private readonly string _direction;
            private readonly string? _protocol;

            public MessageProcessingTimer(SignalRMetrics metrics, Stopwatch stopwatch, string messageType, string direction, string? protocol = null)
            {
                _metrics = metrics;
                _stopwatch = stopwatch;
                _messageType = messageType;
                _direction = direction;
                _protocol = protocol;
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                var tags = new TagList
                {
                    { "message_type", _messageType },
                    { "direction", _direction }
                };

                if (!string.IsNullOrEmpty(_protocol))
                {
                    tags.Add("protocol", _protocol);
                }

                _metrics.MessageProcessingDuration.Record(_stopwatch.ElapsedMilliseconds, tags);
            }
        }
    }
}
