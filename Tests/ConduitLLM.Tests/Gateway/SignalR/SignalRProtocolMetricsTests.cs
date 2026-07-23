using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Filters;
using ConduitLLM.Gateway.Metrics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Moq;

namespace ConduitLLM.Tests.Gateway.SignalR
{
    /// <summary>
    /// Unit tests for SignalR protocol metrics tracking
    /// </summary>
    public class SignalRProtocolMetricsTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ISignalRMetrics _metrics;
        private readonly MeterListener _meterListener;
        private readonly Dictionary<string, List<(Dictionary<string, object> Tags, long Value)>> _counterMeasurements;
        private readonly Dictionary<string, List<(Dictionary<string, object> Tags, double Value)>> _histogramMeasurements;

        public SignalRProtocolMetricsTests()
        {
            var services = new ServiceCollection();
            services.AddMetrics(); // Registers IMeterFactory
            services.AddSingleton<ISignalRMetrics, SignalRMetrics>();

            _serviceProvider = services.BuildServiceProvider();
            _metrics = _serviceProvider.GetRequiredService<ISignalRMetrics>();

            _counterMeasurements = new Dictionary<string, List<(Dictionary<string, object>, long)>>();
            _histogramMeasurements = new Dictionary<string, List<(Dictionary<string, object>, double)>>();

            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "ConduitLLM.SignalR")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                if (!_counterMeasurements.ContainsKey(instrument.Name))
                {
                    _counterMeasurements[instrument.Name] = new List<(Dictionary<string, object>, long)>();
                }
                var tagDict = ConvertTagsToDict(tags);
                _counterMeasurements[instrument.Name].Add((tagDict, measurement));
            });

            _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                if (!_histogramMeasurements.ContainsKey(instrument.Name))
                {
                    _histogramMeasurements[instrument.Name] = new List<(Dictionary<string, object>, double)>();
                }
                var tagDict = ConvertTagsToDict(tags);
                _histogramMeasurements[instrument.Name].Add((tagDict, measurement));
            });

            _meterListener.Start();
        }

        private static Dictionary<string, object> ConvertTagsToDict(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            var dict = new Dictionary<string, object>();
            foreach (var tag in tags)
            {
                dict[tag.Key] = tag.Value;
            }
            return dict;
        }

        [Fact]
        public void ConnectionsTotal_Should_Include_Protocol_Tag()
        {
            // Arrange
            var hubName = "VideoGenerationHub";
            var protocol = "messagepack";

            // Act
            _metrics.ConnectionsTotal.Add(1, new TagList { { "hub", hubName }, { "protocol", protocol } });

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.connections.total");
            var measurements = _counterMeasurements["signalr.connections.total"];
            measurements.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == protocol &&
                GetTagValue(m.Tags, "hub") == hubName);
        }

        [Fact]
        public void ActiveConnections_Should_Track_Protocol_Separately()
        {
            // Arrange
            var hubName = "VideoGenerationHub";

            // Act
            _metrics.ActiveConnections.Add(1, new TagList { { "hub", hubName }, { "protocol", "json" } });
            _metrics.ActiveConnections.Add(1, new TagList { { "hub", hubName }, { "protocol", "messagepack" } });
            _metrics.ActiveConnections.Add(-1, new TagList { { "hub", hubName }, { "protocol", "json" } });

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.connections.active");
            var measurements = _counterMeasurements["signalr.connections.active"];

            measurements.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == "json" && m.Value == 1);
            measurements.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == "messagepack" && m.Value == 1);
            measurements.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == "json" && m.Value == -1);
        }

        [Fact]
        public void HubMethodInvocations_Should_Include_Protocol_Tag()
        {
            // Arrange
            var hubName = "VideoGenerationHub";
            var methodName = "SubscribeToTask";
            var protocol = "messagepack";

            // Act
            using (_metrics.RecordHubMethodInvocation(hubName, methodName, null, protocol))
            {
                // Simulate method execution
            }

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.hub.method.invocations");
            var invocations = _counterMeasurements["signalr.hub.method.invocations"];
            invocations.Should().Contain(m =>
                GetTagValue(m.Tags, "hub") == hubName &&
                GetTagValue(m.Tags, "method") == methodName &&
                GetTagValue(m.Tags, "protocol") == protocol);

            _histogramMeasurements.Should().ContainKey("signalr.hub.method.duration");
            var durations = _histogramMeasurements["signalr.hub.method.duration"];
            durations.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == protocol);
        }

        [Fact]
        public void MessageProcessing_Should_Include_Protocol_Tag()
        {
            // Arrange
            var messageType = "taskProgress";
            var direction = "sent";
            var protocol = "messagepack";

            // Act
            using (_metrics.RecordMessageProcessing(messageType, direction, protocol))
            {
                // Simulate message processing
            }

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.messages.sent");
            var messages = _counterMeasurements["signalr.messages.sent"];
            messages.Should().Contain(m =>
                GetTagValue(m.Tags, "message_type") == messageType &&
                GetTagValue(m.Tags, "direction") == direction &&
                GetTagValue(m.Tags, "protocol") == protocol);

            _histogramMeasurements.Should().ContainKey("signalr.message.processing.duration");
            var durations = _histogramMeasurements["signalr.message.processing.duration"];
            durations.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == protocol);
        }

        [Theory]
        [InlineData("json")]
        [InlineData("messagepack")]
        public void ConnectionErrors_Should_Track_Protocol(string protocol)
        {
            // Arrange
            var hubName = "VideoGenerationHub";
            var errorType = "TimeoutException";

            // Act
            _metrics.ConnectionErrors.Add(1, new TagList
            {
                { "hub", hubName },
                { "protocol", protocol },
                { "error_type", errorType }
            });

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.connections.errors");
            var errors = _counterMeasurements["signalr.connections.errors"];
            errors.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == protocol &&
                GetTagValue(m.Tags, "error_type") == errorType);
        }

        [Fact]
        public void Metrics_Should_Differentiate_Json_And_MessagePack_Traffic()
        {
            // Arrange
            var hubName = "VideoGenerationHub";

            // Act - Simulate mixed protocol traffic
            _metrics.ConnectionsTotal.Add(5, new TagList { { "hub", hubName }, { "protocol", "json" } });
            _metrics.ConnectionsTotal.Add(10, new TagList { { "hub", hubName }, { "protocol", "messagepack" } });

            using (_metrics.RecordHubMethodInvocation(hubName, "Method1", null, "json")) { }
            using (_metrics.RecordHubMethodInvocation(hubName, "Method2", null, "messagepack")) { }
            using (_metrics.RecordHubMethodInvocation(hubName, "Method3", null, "messagepack")) { }

            // Assert
            var connections = _counterMeasurements["signalr.connections.total"];
            var jsonConnections = connections.Where(m => GetTagValue(m.Tags, "protocol") == "json").Sum(m => m.Value);
            var messagePackConnections = connections.Where(m => GetTagValue(m.Tags, "protocol") == "messagepack").Sum(m => m.Value);

            jsonConnections.Should().Be(5);
            messagePackConnections.Should().Be(10);

            var invocations = _counterMeasurements["signalr.hub.method.invocations"];
            var jsonInvocations = invocations.Count(m => GetTagValue(m.Tags, "protocol") == "json");
            var messagePackInvocations = invocations.Count(m => GetTagValue(m.Tags, "protocol") == "messagepack");

            jsonInvocations.Should().Be(1);
            messagePackInvocations.Should().Be(2);
        }

        [Fact]
        public void HubErrors_Should_Include_Protocol_Tag()
        {
            // Arrange
            var hubName = "VideoGenerationHub";
            var methodName = "SubscribeToTask";
            var protocol = "messagepack";
            var errorType = "HubException";

            // Act
            _metrics.HubErrors.Add(1, new TagList
            {
                { "hub", hubName },
                { "method", methodName },
                { "protocol", protocol },
                { "error_type", errorType }
            });

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.hub.errors");
            var errors = _counterMeasurements["signalr.hub.errors"];
            errors.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == protocol &&
                GetTagValue(m.Tags, "hub") == hubName &&
                GetTagValue(m.Tags, "method") == methodName &&
                GetTagValue(m.Tags, "error_type") == errorType);
        }

        [Fact]
        public void Protocol_Tag_Should_Default_To_Json_When_Not_Specified()
        {
            // Arrange
            var hubName = "VideoGenerationHub";
            var methodName = "SomeMethod";

            // Act - Call without protocol parameter (should default to null/not set)
            using (_metrics.RecordHubMethodInvocation(hubName, methodName, null, null))
            {
                // Simulate method execution
            }

            // Assert
            _counterMeasurements.Should().ContainKey("signalr.hub.method.invocations");
            var invocations = _counterMeasurements["signalr.hub.method.invocations"];

            // Should have an invocation without protocol tag
            invocations.Should().Contain(m =>
                GetTagValue(m.Tags, "hub") == hubName &&
                GetTagValue(m.Tags, "method") == methodName &&
                GetTagValue(m.Tags, "protocol") == null);
        }

        [Theory]
        [InlineData("json")]
        [InlineData("messagepack")]
        public async Task MetricsFilter_Should_Not_Guess_Protocol_And_Should_Balance_Active_Connection_Tags(
            string negotiatedProtocol)
        {
            var items = new Dictionary<object, object?>
            {
                // A protocol value from another component must not become an unverified metric label.
                ["Protocol"] = negotiatedProtocol
            };
            var callerContext = new Mock<HubCallerContext>();
            callerContext.SetupGet(x => x.ConnectionId).Returns($"{negotiatedProtocol}-connection");
            callerContext.SetupGet(x => x.Items).Returns(items);
            callerContext.SetupGet(x => x.Features).Returns(new FeatureCollection());

            var lifetimeContext = new HubLifetimeContext(
                callerContext.Object,
                _serviceProvider,
                new TestHub());
            var filter = new SignalRMetricsFilter(
                NullLogger<SignalRMetricsFilter>.Instance,
                _metrics);

            await filter.OnConnectedAsync(lifetimeContext, _ => Task.CompletedTask);
            await filter.OnDisconnectedAsync(lifetimeContext, null, (_, _) => Task.CompletedTask);

            var connections = _counterMeasurements["signalr.connections.total"];
            connections.Should().ContainSingle(m =>
                GetTagValue(m.Tags, "hub") == nameof(TestHub) &&
                !m.Tags.ContainsKey("protocol"));

            var active = _counterMeasurements["signalr.connections.active"]
                .Where(m => GetTagValue(m.Tags, "hub") == nameof(TestHub))
                .ToList();
            active.Select(m => m.Value).Should().Equal(1, -1);
            active.Should().OnlyContain(m => !m.Tags.ContainsKey("protocol"));
            active[0].Tags.Keys.Should().BeEquivalentTo(active[1].Tags.Keys);
        }

        [Fact]
        public void RecordHubMethodInvocation_Should_Track_Duration_With_Protocol()
        {
            // Arrange
            var hubName = "VideoGenerationHub";
            var methodName = "SubscribeToTask";
            var protocol = "messagepack";

            // Act
            using (_metrics.RecordHubMethodInvocation(hubName, methodName, 123, protocol))
            {
                System.Threading.Thread.Sleep(10); // Simulate work
            }

            // Assert
            _histogramMeasurements.Should().ContainKey("signalr.hub.method.duration");
            var durations = _histogramMeasurements["signalr.hub.method.duration"];
            durations.Should().Contain(m =>
                GetTagValue(m.Tags, "protocol") == protocol &&
                GetTagValue(m.Tags, "hub") == hubName &&
                GetTagValue(m.Tags, "method") == methodName &&
                m.Value >= 10); // Should have recorded at least 10ms
        }

        private string GetTagValue(Dictionary<string, object> tags, string key)
        {
            if (tags.TryGetValue(key, out var value))
            {
                return value?.ToString();
            }
            return null;
        }

        public void Dispose()
        {
            _meterListener?.Dispose();
            _serviceProvider?.Dispose();
            (_metrics as IDisposable)?.Dispose();
        }

        private sealed class TestHub : Hub;
    }
}
