using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Middleware;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Http.Middleware
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    public class VirtualKeyRateLimitMiddlewareTests
    {
        private readonly Mock<IVirtualKeyRateLimitService> _mockService = new();
        private bool _nextCalled;

        private VirtualKeyRateLimitMiddleware CreateMiddleware()
        {
            return new VirtualKeyRateLimitMiddleware(
                next: _ => { _nextCalled = true; return Task.CompletedTask; },
                rateLimitService: _mockService.Object,
                logger: NullLogger<VirtualKeyRateLimitMiddleware>.Instance);
        }

        private static DefaultHttpContext NewContext(string path = "/v1/chat/completions")
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Path = path;
            ctx.Response.Body = new MemoryStream();
            return ctx;
        }

        [Theory]
        [InlineData("/health")]
        [InlineData("/health/ready")]
        [InlineData("/metrics")]
        [InlineData("/v1/conduit/media/public/foo")]
        [InlineData("/hubs/images")]
        public async Task Skips_excluded_paths_without_calling_service(string path)
        {
            var ctx = NewContext(path);
            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeTrue();
            _mockService.Verify(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact]
        public async Task Passes_through_when_no_virtual_key_hash_in_items()
        {
            // Backend-scheme requests (CONDUIT_API_TO_API_BACKEND_AUTH_KEY) authenticate but
            // don't stash VirtualKey.KeyHash, so they bypass rate limiting.
            var ctx = NewContext();
            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeTrue();
            _mockService.Verify(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact]
        public async Task Passes_through_when_both_limits_null()
        {
            // null/null = unlimited per business rule. No Redis round-trip.
            var ctx = NewContext();
            ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
            ctx.Items["VirtualKey.RateLimitRpm"] = null;
            ctx.Items["VirtualKey.RateLimitRpd"] = null;

            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeTrue();
            _mockService.Verify(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact]
        public async Task Passes_through_when_both_limits_zero_or_negative()
        {
            var ctx = NewContext();
            ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
            ctx.Items["VirtualKey.RateLimitRpm"] = 0;
            ctx.Items["VirtualKey.RateLimitRpd"] = 0;

            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeTrue();
            _mockService.Verify(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact]
        public async Task Allows_request_and_sets_headers_when_service_returns_allowed()
        {
            var ctx = NewContext();
            ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
            ctx.Items["VirtualKey.RateLimitRpm"] = 60;
            ctx.Items["VirtualKey.RateLimitRpd"] = null;

            _mockService.Setup(s => s.CheckRateLimitAsync("hash-abc", 60, null))
                .ReturnsAsync(new RateLimitCheckResult
                {
                    IsAllowed = true,
                    Limit = 60,
                    RequestsRemaining = 42,
                    ResetsAt = DateTime.UtcNow.AddSeconds(30),
                    LimitType = "RPM"
                });

            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeTrue();
            ctx.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("60");
            ctx.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("42");
            ctx.Response.Headers["X-RateLimit-Scope"].ToString().Should().Be("RPM");
            ctx.Response.Headers.ContainsKey("X-RateLimit-Reset").Should().BeTrue();
            ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Returns_429_with_Retry_After_when_service_rejects()
        {
            var ctx = NewContext();
            ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
            ctx.Items["VirtualKey.RateLimitRpm"] = 10;
            ctx.Items["VirtualKey.RateLimitRpd"] = null;

            _mockService.Setup(s => s.CheckRateLimitAsync("hash-abc", 10, null))
                .ReturnsAsync(new RateLimitCheckResult
                {
                    IsAllowed = false,
                    Limit = 10,
                    RequestsRemaining = 0,
                    ResetsAt = DateTime.UtcNow.AddSeconds(45),
                    LimitType = "RPM"
                });

            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeFalse();
            ctx.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            ctx.Response.Headers.ContainsKey("Retry-After").Should().BeTrue();
            int.Parse(ctx.Response.Headers["Retry-After"].ToString()).Should().BeGreaterThan(0);
            ctx.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
            ctx.Response.Headers["X-RateLimit-Scope"].ToString().Should().Be("RPM");
            ctx.Response.ContentType.Should().Contain("application/json");
        }

        [Fact]
        public async Task Fails_open_when_service_throws()
        {
            // If Redis is down, we'd rather let the request through than tank the gateway.
            var ctx = NewContext();
            ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
            ctx.Items["VirtualKey.RateLimitRpm"] = 60;

            _mockService.Setup(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .ThrowsAsync(new InvalidOperationException("Redis is required for secure distributed rate limiting"));

            await CreateMiddleware().InvokeAsync(ctx);

            _nextCalled.Should().BeTrue();
            ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public async Task Forwards_both_RPM_and_RPD_when_both_configured()
        {
            var ctx = NewContext();
            ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
            ctx.Items["VirtualKey.RateLimitRpm"] = 60;
            ctx.Items["VirtualKey.RateLimitRpd"] = 10000;

            _mockService.Setup(s => s.CheckRateLimitAsync("hash-abc", 60, 10000))
                .ReturnsAsync(new RateLimitCheckResult { IsAllowed = true, Limit = 60, RequestsRemaining = 50, LimitType = "RPM" });

            await CreateMiddleware().InvokeAsync(ctx);

            _mockService.Verify(s => s.CheckRateLimitAsync("hash-abc", 60, 10000), Times.Once);
            _nextCalled.Should().BeTrue();
        }
    }
}
