using System;
using System.Collections.Generic;
using System.Diagnostics;

using ConduitLLM.Core.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Core.Services
{
    public class CorrelationContextServiceTests : IDisposable
    {
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<ILogger<CorrelationContextService>> _mockLogger;
        private readonly CorrelationContextService _service;
        private readonly Activity? _previousActivity;

        public CorrelationContextServiceTests()
        {
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockLogger = new Mock<ILogger<CorrelationContextService>>();
            _service = new CorrelationContextService(_mockHttpContextAccessor.Object, _mockLogger.Object);
            
            // Save current activity to restore later
            _previousActivity = Activity.Current;
        }

        public void Dispose()
        {
            // Restore previous activity
            Activity.Current = _previousActivity;
        }

        [Fact]
        public void CorrelationId_FromHttpContext_ReturnsCorrectValue()
        {
            // Arrange
            var expectedCorrelationId = "http-context-correlation-id";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["CorrelationId"] = expectedCorrelationId;
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var correlationId = _service.CorrelationId;

            // Assert
            Assert.Equal(expectedCorrelationId, correlationId);
        }

        [Fact]
        public void CorrelationId_NoHttpContextUsesTraceIdentifier_ReturnsTraceIdentifier()
        {
            // Arrange
            var expectedTraceId = "trace-identifier-123";
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = expectedTraceId;
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var correlationId = _service.CorrelationId;

            // Assert
            Assert.Equal(expectedTraceId, correlationId);
        }

        [Fact]
        public void CorrelationId_FromActivity_ReturnsCorrectValue()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null!);
            var activity = new Activity("TestOperation").Start();
            activity.SetBaggage("correlation.id", "activity-correlation-id");
            Activity.Current = activity;

            // Act
            var correlationId = _service.CorrelationId;

            // Assert
            Assert.Equal("activity-correlation-id", correlationId);

            // Cleanup
            activity.Stop();
        }

        [Fact]
        public void CorrelationId_NoContext_ReturnsNull()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null!);
            Activity.Current = null;

            // Act
            var correlationId = _service.CorrelationId;

            // Assert
            Assert.Null(correlationId);
        }

        [Fact]
        public void TraceId_FromActivity_ReturnsCorrectValue()
        {
            // Arrange
            var activity = new Activity("TestOperation").Start();
            Activity.Current = activity;

            // Act
            var traceId = _service.TraceId;

            // Assert
            Assert.NotNull(traceId);
            Assert.Equal(activity.TraceId.ToString(), traceId);

            // Cleanup
            activity.Stop();
        }

        [Fact]
        public void SpanId_FromActivity_ReturnsCorrectValue()
        {
            // Arrange
            var activity = new Activity("TestOperation").Start();
            Activity.Current = activity;

            // Act
            var spanId = _service.SpanId;

            // Assert
            Assert.NotNull(spanId);
            Assert.Equal(activity.SpanId.ToString(), spanId);

            // Cleanup
            activity.Stop();
        }

        [Fact]
        public void GetPropagationHeaders_WithCorrelationId_IncludesCorrelationHeaders()
        {
            // Arrange
            var correlationId = "test-correlation-123";
            var httpContext = new DefaultHttpContext();
            httpContext.Items["CorrelationId"] = correlationId;
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            Assert.Contains("X-Correlation-ID", headers.Keys);
            Assert.Contains("X-Request-ID", headers.Keys);
            Assert.Equal(correlationId, headers["X-Correlation-ID"]);
            Assert.Equal(correlationId, headers["X-Request-ID"]);
        }

        [Fact]
        public void GetPropagationHeaders_WithActivity_IncludesTraceContext()
        {
            // Arrange
            var activity = new Activity("TestOperation").Start();
            Activity.Current = activity;
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null!);

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            Assert.Contains("traceparent", headers.Keys);
            var traceparent = headers["traceparent"];
            Assert.Contains(activity.TraceId.ToString(), traceparent);
            Assert.Contains(activity.SpanId.ToString(), traceparent);

            // Cleanup
            activity.Stop();
        }

        [Fact]
        public void GetPropagationHeaders_WithActivityBaggage_IncludesContextHeaders()
        {
            // Arrange
            var activity = new Activity("TestOperation").Start();
            activity.SetBaggage("context.user-id", "user123");
            activity.SetBaggage("context.tenant-id", "tenant456");
            activity.SetBaggage("other.data", "should-not-include");
            Activity.Current = activity;
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null!);

            // Act
            var headers = _service.GetPropagationHeaders();

            // Assert
            Assert.Contains("X-Context-context.user-id", headers.Keys);
            Assert.Contains("X-Context-context.tenant-id", headers.Keys);
            Assert.DoesNotContain("X-Context-other.data", headers.Keys);
            Assert.Equal("user123", headers["X-Context-context.user-id"]);
            Assert.Equal("tenant456", headers["X-Context-context.tenant-id"]);

            // Cleanup
            activity.Stop();
        }

        [Fact]
        public void CreateScope_SetsCorrelationId()
        {
            // Arrange
            var scopeCorrelationId = "scope-correlation-id";
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null!);

            // Act
            string? correlationIdInScope = null;
            using (var scope = _service.CreateScope(scopeCorrelationId))
            {
                correlationIdInScope = _service.CorrelationId;
            }
            var correlationIdAfterScope = _service.CorrelationId;

            // Assert
            Assert.Equal(scopeCorrelationId, correlationIdInScope);
            Assert.Null(correlationIdAfterScope);
        }

        [Fact]
        public void CreateScope_WithActivity_SetsBaggage()
        {
            // Arrange
            var activity = new Activity("TestOperation").Start();
            Activity.Current = activity;
            var scopeCorrelationId = "scope-correlation-id";

            // Act
            string? baggageInScope = null;
            using (var scope = _service.CreateScope(scopeCorrelationId))
            {
                baggageInScope = activity.GetBaggageItem("correlation.id");
            }
            var baggageAfterScope = activity.GetBaggageItem("correlation.id");

            // Assert
            Assert.Equal(scopeCorrelationId, baggageInScope);
            Assert.Null(baggageAfterScope);

            // Cleanup
            activity.Stop();
        }

        [Fact]
        public void CreateScope_Nested_RestoresPreviousValue()
        {
            // Arrange
            var outerCorrelationId = "outer-correlation";
            var innerCorrelationId = "inner-correlation";
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null!);

            // Act
            string? correlationIdInOuterScope = null;
            string? correlationIdInInnerScope = null;
            string? correlationIdAfterInnerScope = null;
            using (var outerScope = _service.CreateScope(outerCorrelationId))
            {
                correlationIdInOuterScope = _service.CorrelationId;
                
                using (var innerScope = _service.CreateScope(innerCorrelationId))
                {
                    correlationIdInInnerScope = _service.CorrelationId;
                }
                
                correlationIdAfterInnerScope = _service.CorrelationId;
            }
            var correlationIdAfterAllScopes = _service.CorrelationId;

            // Assert
            Assert.Equal(outerCorrelationId, correlationIdInOuterScope);
            Assert.Equal(innerCorrelationId, correlationIdInInnerScope);
            Assert.Equal(outerCorrelationId, correlationIdAfterInnerScope);
            Assert.Null(correlationIdAfterAllScopes);
        }
    }
}