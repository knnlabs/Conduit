using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConduitLLM.Http.SignalR.Metrics
{
    /// <summary>
    /// OpenTelemetry metrics for SignalR operations
    /// </summary>
    public class SignalRMetrics : IDisposable
    {
        private readonly Meter _meter;
        
        // Counters
        private readonly Counter<long> _messagesDelivered;
        private readonly Counter<long> _messagesFailed;
        private readonly Counter<long> _messagesAcknowledged;
        private readonly Counter<long> _messagesTimedOut;
        private readonly Counter<long> _connectionsCreated;
        private readonly Counter<long> _connectionsFailed;
        private readonly Counter<long> _messagesQueued;
        private readonly Counter<long> _messagesBatched;
        private readonly Counter<long> _batchesSent;
        
        // Histograms
        private readonly Histogram<double> _messageDeliveryDuration;
        private readonly Histogram<double> _messageAcknowledgmentDuration;
        private readonly Histogram<double> _connectionDuration;
        private readonly Histogram<double> _batchSize;
        private readonly Histogram<double> _batchLatency;
        private readonly Histogram<double> _queueProcessingDuration;
        
        // Gauges (UpDownCounters)
        private readonly UpDownCounter<long> _activeConnections;
        private readonly UpDownCounter<long> _activeGroups;
        private readonly UpDownCounter<long> _queueDepth;
        private readonly UpDownCounter<long> _deadLetterQueueDepth;
        private readonly UpDownCounter<long> _pendingAcknowledgments;
        private readonly UpDownCounter<long> _pendingBatches;

        // ObservableGauges for real-time metrics - removed as they were unused

        public SignalRMetrics(IMeterFactory meterFactory)
        {
            _meter = meterFactory.Create("ConduitLLM.SignalR", "1.0.0");

            // Initialize Counters
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

            _connectionsCreated = _meter.CreateCounter<long>(
                "signalr.connections.created",
                "connections",
                "Number of connections created");

            _connectionsFailed = _meter.CreateCounter<long>(
                "signalr.connections.failed",
                "connections",
                "Number of failed connection attempts");

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

            // Initialize Histograms
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

            // Initialize Gauges
            _activeConnections = _meter.CreateUpDownCounter<long>(
                "signalr.connections.active",
                "connections",
                "Number of active connections");

            _activeGroups = _meter.CreateUpDownCounter<long>(
                "signalr.groups.active",
                "groups",
                "Number of active groups");

            _queueDepth = _meter.CreateUpDownCounter<long>(
                "signalr.queue.depth",
                "messages",
                "Number of messages in the queue");

            _deadLetterQueueDepth = _meter.CreateUpDownCounter<long>(
                "signalr.queue.dead_letter.depth",
                "messages",
                "Number of messages in the dead letter queue");

            _pendingAcknowledgments = _meter.CreateUpDownCounter<long>(
                "signalr.acknowledgments.pending",
                "acknowledgments",
                "Number of pending acknowledgments");

            _pendingBatches = _meter.CreateUpDownCounter<long>(
                "signalr.batches.pending",
                "batches",
                "Number of pending message batches");
        }

        // Counter methods
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
            
            if (success)
                _connectionsCreated.Add(1, tags);
            else
                _connectionsFailed.Add(1, tags);
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

        // Histogram methods
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

        // Gauge methods
        public void UpdateActiveConnections(string hub, int delta)
        {
            _activeConnections.Add(delta, new TagList { { "hub", hub } });
        }

        public void UpdateActiveGroups(string hub, int delta)
        {
            _activeGroups.Add(delta, new TagList { { "hub", hub } });
        }

        public void UpdateQueueDepth(int delta)
        {
            _queueDepth.Add(delta);
        }

        public void UpdateDeadLetterQueueDepth(int delta)
        {
            _deadLetterQueueDepth.Add(delta);
        }

        public void UpdatePendingAcknowledgments(int delta)
        {
            _pendingAcknowledgments.Add(delta);
        }

        public void UpdatePendingBatches(int delta)
        {
            _pendingBatches.Add(delta);
        }

        // Activity source for distributed tracing
        public static readonly ActivitySource ActivitySource = new("ConduitLLM.SignalR", "1.0.0");

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
    }
}