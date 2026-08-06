using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Messaging;

namespace ConduitLLM.Tests.Messaging
{
    /// <summary>
    /// Lightweight <see cref="IEventContext"/> test double for invoking
    /// <see cref="IEventHandler{TEvent}.HandleAsync"/> directly in unit tests (epic #909).
    /// Records follow-on publishes and scheduled publishes and lets a test seed headers,
    /// message id, correlation id, and a cancellation token.
    /// </summary>
    public sealed class TestEventContext : IEventContext
    {
        private readonly Dictionary<string, object> _headers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Events published via <see cref="PublishAsync{TEvent}"/>.</summary>
        public List<object> Published { get; } = new();

        /// <summary>(deliveryTime, event) pairs scheduled via <see cref="SchedulePublishAsync{TEvent}"/>.</summary>
        public List<(DateTime DeliveryTime, object Event)> Scheduled { get; } = new();

        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public Guid? MessageId { get; set; } = Guid.NewGuid();
        public string? CorrelationId { get; set; }

        public void SetHeader(string key, object value) => _headers[key] = value;

        public bool TryGetHeader(string key, out object? value)
        {
            if (_headers.TryGetValue(key, out var v))
            {
                value = v;
                return true;
            }
            value = null;
            return false;
        }

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Published.Add(@event);
            return Task.CompletedTask;
        }

        public Task SchedulePublishAsync<TEvent>(DateTime deliveryTime, TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Scheduled.Add((deliveryTime, @event));
            return Task.CompletedTask;
        }
    }
}
