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

        // Additional metrics from new implementation
        private readonly Counter<long> _messagesDelivered;
        private readonly Counter<long> _messagesFailed;
        private readonly Counter<long> _messagesAcknowledged;
        private readonly Counter<long> _messagesTimedOut;
        private readonly Counter<long> _messagesQueued;
        private readonly Counter<long> _messagesBatched;
        private readonly Counter<long> _batchesSent;
        private readonly Counter<long> _queueMessagesClaimed;
        private readonly Counter<long> _queueMessageRetries;

        // Additional histograms
        private readonly Histogram<double> _messageDeliveryDuration;
        private readonly Histogram<double> _messageAcknowledgmentDuration;
        private readonly Histogram<double> _connectionDuration;
        private readonly Histogram<double> _batchSize;
        private readonly Histogram<double> _batchLatency;
        private readonly Histogram<double> _queueProcessingDuration;

        // Additional gauges
        private readonly ObservableGauge<long> _queueDepth;
        private readonly ObservableGauge<long> _deadLetterQueueDepth;
        private readonly ObservableGauge<double> _oldestPendingMessageAge;
        private readonly UpDownCounter<long> _pendingAcknowledgments;
        private readonly UpDownCounter<long> _pendingBatches;
        private long _queueDepthValue;
        private long _deadLetterQueueDepthValue;
        private double _oldestPendingMessageAgeSeconds;

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

            // Initialize additional counters
            _messagesDelivered = _meter.CreateCounter<long>(
                "signalr.messages.delivered",
                "messages",
                "Number of messages successfully delivered");

            _messagesFailed = _meter.CreateCounter<long>(
                "signalr.messages.failed",
                "messages",
                "Number of messages that failed delivery");

            _messagesAcknowledged = _meter.CreateCounter<long>(
                "signalr.messages.acknowledged",
                "messages",
                "Number of messages acknowledged by clients");

            _messagesTimedOut = _meter.CreateCounter<long>(
                "signalr.messages.timed_out",
                "messages",
                "Number of messages that timed out waiting for acknowledgment");

            _messagesQueued = _meter.CreateCounter<long>(
                "signalr.messages.queued",
                "messages",
                "Number of messages added to the queue");

            _messagesBatched = _meter.CreateCounter<long>(
                "signalr.messages.batched",
                "messages",
                "Number of messages added to batches");

            _batchesSent = _meter.CreateCounter<long>(
                "signalr.batches.sent",
                "batches",
                "Number of message batches sent");

            _queueMessagesClaimed = _meter.CreateCounter<long>(
                "signalr.queue.claimed",
                "messages",
                "Number of abandoned stream messages claimed by a live consumer");

            _queueMessageRetries = _meter.CreateCounter<long>(
                "signalr.queue.retries",
                "messages",
                "Number of failed queue deliveries scheduled for retry");

            // Initialize additional histograms
            _messageDeliveryDuration = _meter.CreateHistogram<double>(
                "signalr.message.delivery.duration",
                "milliseconds",
                "Duration of message delivery");

            _messageAcknowledgmentDuration = _meter.CreateHistogram<double>(
                "signalr.message.acknowledgment.duration",
                "milliseconds",
                "Duration from message send to acknowledgment");

            _connectionDuration = _meter.CreateHistogram<double>(
                "signalr.connection.duration",
                "seconds",
                "Duration of connections");

            _batchSize = _meter.CreateHistogram<double>(
                "signalr.batch.size",
                "messages",
                "Number of messages per batch");

            _batchLatency = _meter.CreateHistogram<double>(
                "signalr.batch.latency",
                "milliseconds",
                "Time messages spend waiting in batch");

            _queueProcessingDuration = _meter.CreateHistogram<double>(
                "signalr.queue.processing.duration",
                "milliseconds",
                "Duration of queue processing operations");

            // Initialize additional gauges
            _queueDepth = _meter.CreateObservableGauge(
                "signalr.queue.depth",
                () => Volatile.Read(ref _queueDepthValue),
                "messages",
                "Number of messages in the queue");

            _deadLetterQueueDepth = _meter.CreateObservableGauge(
                "signalr.queue.dead_letter.depth",
                () => Volatile.Read(ref _deadLetterQueueDepthValue),
                "messages",
                "Number of messages in the dead letter queue");

            _oldestPendingMessageAge = _meter.CreateObservableGauge(
                "signalr.queue.pending.oldest_age",
                () => Volatile.Read(ref _oldestPendingMessageAgeSeconds),
                "seconds",
                "Age of the oldest message in the Redis pending-entry list");

            _pendingAcknowledgments = _meter.CreateUpDownCounter<long>(
                "signalr.acknowledgments.pending",
                "acknowledgments",
                "Number of pending acknowledgments");

            _pendingBatches = _meter.CreateUpDownCounter<long>(
                "signalr.batches.pending",
                "batches",
                "Number of pending message batches");
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

        // Additional methods from new implementation
        public void RecordMessageDelivered(string hub, string method, bool success)
        {
            var tags = new TagList
            {
                { "hub", hub },
                { "method", method },
                { "status", success ? "success" : "failure" }
            };

            if (success)
                _messagesDelivered.Add(1, tags);
            else
                _messagesFailed.Add(1, tags);
        }

        public void RecordMessageAcknowledged(string hub, string method)
        {
            _messagesAcknowledged.Add(1, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordMessageTimedOut(string hub, string method)
        {
            _messagesTimedOut.Add(1, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordConnectionCreated(string hub, bool success)
        {
            var tags = new TagList { { "hub", hub } };

            ConnectionsTotal.Add(1, tags);

            if (!success)
                ConnectionErrors.Add(1, tags);
        }

        public void RecordMessageQueued(string hub, string method, int priority)
        {
            _messagesQueued.Add(1, new TagList
            {
                { "hub", hub },
                { "method", method },
                { "priority", priority.ToString() }
            });
        }

        public void RecordMessageBatched(string hub, string method)
        {
            _messagesBatched.Add(1, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordBatchSent(string hub, string method, int messageCount)
        {
            _batchesSent.Add(1, new TagList
            {
                { "hub", hub },
                { "method", method }
            });

            _batchSize.Record(messageCount, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordMessageDeliveryDuration(string hub, string method, double durationMs)
        {
            _messageDeliveryDuration.Record(durationMs, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordAcknowledgmentDuration(string hub, string method, double durationMs)
        {
            _messageAcknowledgmentDuration.Record(durationMs, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordConnectionDuration(string hub, double durationSeconds)
        {
            _connectionDuration.Record(durationSeconds, new TagList
            {
                { "hub", hub }
            });
        }

        public void RecordBatchLatency(string hub, string method, double latencyMs)
        {
            _batchLatency.Record(latencyMs, new TagList
            {
                { "hub", hub },
                { "method", method }
            });
        }

        public void RecordQueueProcessingDuration(double durationMs, int messagesProcessed)
        {
            _queueProcessingDuration.Record(durationMs, new TagList
            {
                { "messages_processed", messagesProcessed.ToString() }
            });
        }

        public void UpdateActiveConnections(string hub, int delta)
        {
            ActiveConnections.Add(delta, new TagList { { "hub", hub } });
        }

        public void UpdateActiveGroups(string hub, int delta)
        {
            ActiveGroups.Add(delta, new TagList { { "hub", hub } });
        }

        public void UpdateQueueDepth(int value)
        {
            Interlocked.Exchange(ref _queueDepthValue, value);
        }

        public void UpdateDeadLetterQueueDepth(int value)
        {
            Interlocked.Exchange(ref _deadLetterQueueDepthValue, value);
        }

        public void UpdateOldestPendingMessageAge(double ageSeconds)
        {
            Interlocked.Exchange(ref _oldestPendingMessageAgeSeconds, ageSeconds);
        }

        public void RecordQueueMessagesClaimed(int count)
        {
            _queueMessagesClaimed.Add(count);
        }

        public void RecordQueueMessageRetry()
        {
            _queueMessageRetries.Add(1);
        }

        public void UpdatePendingAcknowledgments(int delta)
        {
            _pendingAcknowledgments.Add(delta);
        }

        public void UpdatePendingBatches(int delta)
        {
            _pendingBatches.Add(delta);
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
