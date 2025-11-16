using System.Diagnostics;

using ConduitLLM.Core.Services;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    [Trait("Category", "Unit")]
    [Trait("Phase", "2")]
    [Trait("Component", "Core")]
    public class CorrelationContextServiceTests : TestBase
    {
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<ILogger<CorrelationContextService>> _loggerMock;
        private readonly CorrelationContextService _service;

        public CorrelationContextServiceTests(ITestOutputHelper output) : base(output)
        {
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _loggerMock = CreateLogger<CorrelationContextService>();
            _service = new CorrelationContextService(_httpContextAccessorMock.Object, _loggerMock.Object);
        }

        [Fact]
        public void CorrelationId_WithHttpContextItem_ReturnsItemValue()
        {
            // Arrange
            var expectedId = "http-context-correlation-id";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["CorrelationId"] = expectedId;
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var result = _service.CorrelationId;

            // Assert
            result.Should().Be(expectedId);
        }

        [Fact]
        public void CorrelationId_WithoutHttpContextItem_FallsBackToTraceIdentifier()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = "trace-id-123";
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var result = _service.CorrelationId;

            // Assert
            result.Should().Be("trace-id-123");
        }

        [Fact]
        public void CorrelationId_WithNoHttpContext_ReturnsNull()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

            // Act
            var result = _service.CorrelationId;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void CorrelationId_WithActivity_ReturnsBaggageItem()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            
            var activity = new Activity("TestActivity");
            activity.SetBaggage("correlation.id", "activity-correlation-id");
            activity.Start();

            try
            {
                // Act
                var result = _service.CorrelationId;

                // Assert
                result.Should().Be("activity-correlation-id");
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void CreateScope_SetsCorrelationId_InAsyncLocal()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var correlationId = "scoped-correlation-id";

            // Act & Assert
            _service.CorrelationId.Should().BeNull();

            using (var scope = _service.CreateScope(correlationId))
            {
                _service.CorrelationId.Should().Be(correlationId);
            }

            _service.CorrelationId.Should().BeNull();
        }

        [Fact]
        public void CreateScope_WithActivity_SetsBaggageItem()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var correlationId = "scoped-correlation-id";
            
            var activity = new Activity("TestActivity");
            activity.Start();

            try
            {
                // Act
                using (var scope = _service.CreateScope(correlationId))
                {
                    // Assert - baggage should be set
                    activity.GetBaggageItem("correlation.id").Should().Be(correlationId);
                }

                // Assert - baggage should be cleared
                activity.GetBaggageItem("correlation.id").Should().BeNull();
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void CreateScope_Nested_RestoresPreviousValue()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var outerCorrelationId = "outer-correlation-id";
            var innerCorrelationId = "inner-correlation-id";

            // Act & Assert
            using (var outerScope = _service.CreateScope(outerCorrelationId))
            {
                _service.CorrelationId.Should().Be(outerCorrelationId);

                using (var innerScope = _service.CreateScope(innerCorrelationId))
                {
                    _service.CorrelationId.Should().Be(innerCorrelationId);
                }

                _service.CorrelationId.Should().Be(outerCorrelationId);
            }

            _service.CorrelationId.Should().BeNull();
        }

        [Fact]
        public void TraceId_WithActivity_ReturnsTraceId()
        {
            // Arrange
            var activity = new Activity("TestActivity");
            activity.Start();

            try
            {
                // Act
                var result = _service.TraceId;

                // Assert
                result.Should().NotBeNull();
                result.Should().HaveLength(32); // W3C TraceId format
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void TraceId_WithoutActivity_ReturnsNull()
        {
            // Act
            var result = _service.TraceId;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void SpanId_WithActivity_ReturnsSpanId()
        {
            // Arrange
            var activity = new Activity("TestActivity");
            activity.Start();

            try
            {
                // Act
                var result = _service.SpanId;

                // Assert
                result.Should().NotBeNull();
                result.Should().HaveLength(16); // W3C SpanId format
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void SpanId_WithoutActivity_ReturnsNull()
        {
            // Act
            var result = _service.SpanId;

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetPropagationHeaders_WithCorrelationId_IncludesHeaders()
        {
            // Arrange
            var correlationId = "test-correlation-id";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["CorrelationId"] = correlationId;
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            headers.Should().ContainKey("X-Correlation-ID");
            headers["X-Correlation-ID"].Should().Be(correlationId);
            headers.Should().ContainKey("X-Request-ID");
            headers["X-Request-ID"].Should().Be(correlationId);
        }

        [Fact]
        public void GetPropagationHeaders_WithActivity_IncludesTraceHeaders()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
            
            var activity = new Activity("TestActivity");
            activity.Start();

            try
            {
                // Act
                var headers = _service.GetPropagationHeaders();

                // Assert
                headers.Should().ContainKey("traceparent");
                headers["traceparent"].Should().MatchRegex(@"00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]");
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void GetPropagationHeaders_WithActivityBaggage_IncludesContextHeaders()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
            
            var activity = new Activity("TestActivity");
            activity.SetBaggage("correlation.id", "baggage-correlation-id");
            activity.SetBaggage("context.user-id", "user-123");
            activity.SetBaggage("other.data", "should-not-include");
            activity.Start();

            try
            {
                // Act
                var headers = _service.GetPropagationHeaders();

                // Assert
                headers.Should().ContainKey("X-Context-correlation.id");
                headers["X-Context-correlation.id"].Should().Be("baggage-correlation-id");
                headers.Should().ContainKey("X-Context-context.user-id");
                headers["X-Context-context.user-id"].Should().Be("user-123");
                headers.Should().NotContainKey("X-Context-other.data");
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void GetPropagationHeaders_WithTraceState_IncludesTraceStateHeader()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
            
            var activity = new Activity("TestActivity");
            activity.TraceStateString = "vendor1=value1,vendor2=value2";
            activity.Start();

            try
            {
                // Act
                var headers = _service.GetPropagationHeaders();

                // Assert
                headers.Should().ContainKey("tracestate");
                headers["tracestate"].Should().Be("vendor1=value1,vendor2=value2");
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void GetPropagationHeaders_LogsDebugMessage()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("propagation headers")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetPropagationHeaders_WithNoContext_ReturnsEmptyDictionary()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            headers.Should().NotBeNull();
            headers.Should().BeEmpty();
        }

        [Fact]
        public void CorrelationId_PriorityOrder_HttpContextItemFirst()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Items["CorrelationId"] = "http-context-id";
            httpContext.TraceIdentifier = "trace-id";
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            using (var scope = _service.CreateScope("async-local-id"))
            {
                var activity = new Activity("TestActivity");
                activity.SetBaggage("correlation.id", "activity-id");
                activity.Start();

                try
                {
                    // Act
                    var result = _service.CorrelationId;

                    // Assert - HttpContext item should take precedence
                    result.Should().Be("http-context-id");
                }
                finally
                {
                    activity.Stop();
                }
            }
        }

        [Fact]
        public void CreateScope_WithExistingActivityBaggage_RestoresPreviousValue()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            
            var activity = new Activity("TestActivity");
            activity.SetBaggage("correlation.id", "original-id");
            activity.Start();

            try
            {
                // Act
                using (var scope = _service.CreateScope("new-id"))
                {
                    activity.GetBaggageItem("correlation.id").Should().Be("new-id");
                }

                // Assert - should restore original value
                activity.GetBaggageItem("correlation.id").Should().Be("original-id");
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void GetPropagationHeaders_WithRecordedActivity_SetsCorrectFlag()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
            
            var activitySource = new ActivitySource("TestSource");
            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(listener);

            using var activity = activitySource.StartActivity("TestActivity", ActivityKind.Internal);
            activity.Should().NotBeNull();

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            headers.Should().ContainKey("traceparent");
            headers["traceparent"].Should().EndWith("-01"); // 01 indicates recorded
        }

        [Fact]
        public void GetPropagationHeaders_WithNullBaggageValue_HandlesGracefully()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
            
            var activity = new Activity("TestActivity");
            // Setting baggage with null value might be ignored by Activity
            activity.SetBaggage("context.empty", "");
            activity.SetBaggage("context.valid", "value");
            activity.Start();

            try
            {
                // Act
                var headers = _service.GetPropagationHeaders();

                // Assert
                headers.Should().ContainKey("X-Context-context.valid");
                headers["X-Context-context.valid"].Should().Be("value");
                headers.Should().ContainKey("X-Context-context.empty");
                headers["X-Context-context.empty"].Should().Be("");
                
                // Verify the service handles empty/null values without throwing
                var act = () => _service.GetPropagationHeaders();
                act.Should().NotThrow();
            }
            finally
            {
                activity.Stop();
            }
        }
    }
}