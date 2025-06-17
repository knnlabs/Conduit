using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Options;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Middleware;
using ConduitLLM.WebUI.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.WebUI.Middleware
{
    public class IpFilterMiddlewareTests
    {
        private readonly Mock<ILogger<IpFilterMiddleware>> _mockLogger;
        private readonly Mock<IIpFilterService> _mockIpFilterService;
        private readonly Mock<IOptions<IpFilterOptions>> _mockOptions;
        private readonly Mock<ISecurityConfigurationService> _mockSecurityConfig;
        private readonly Mock<IIpAddressClassifier> _mockIpClassifier;
        private readonly RequestDelegate _nextMock;

        public IpFilterMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<IpFilterMiddleware>>();
            _mockIpFilterService = new Mock<IIpFilterService>();
            _mockOptions = new Mock<IOptions<IpFilterOptions>>();
            _mockSecurityConfig = new Mock<ISecurityConfigurationService>();
            _mockIpClassifier = new Mock<IIpAddressClassifier>();

            _mockOptions.Setup(o => o.Value).Returns(new IpFilterOptions
            {
                Enabled = true,
                DefaultAllow = true,
                BypassForAdminUi = true,
                EnableIpv6 = true,
                ExcludedEndpoints = new() { "/api/v1/health" }
            });

            // Setup default security config
            _mockSecurityConfig.Setup(x => x.AllowPrivateIps).Returns(false);
            _mockSecurityConfig.Setup(x => x.GetIpFilterSettings()).Returns(new IpFilterSettingsDto
            {
                IsEnabled = false,
                WhitelistFilters = new List<IpFilterDto>(),
                BlacklistFilters = new List<IpFilterDto>()
            });

            _nextMock = (HttpContext context) => Task.CompletedTask;
        }

        private IpFilterMiddleware CreateMiddleware(RequestDelegate? next = null)
        {
            return new IpFilterMiddleware(
                next ?? _nextMock,
                _mockLogger.Object,
                _mockOptions.Object,
                _mockSecurityConfig.Object,
                _mockIpClassifier.Object
            );
        }

        [Fact]
        public async Task InvokeAsync_WhenFilteringDisabled_CallsNextMiddleware()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/api/v1/chat");

            var settings = new IpFilterSettingsDto { IsEnabled = false };
            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync()).ReturnsAsync(settings);

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) => { nextCalled = true; return Task.CompletedTask; };
            var middleware = CreateMiddleware(next);

            // Act
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_WhenPathExcluded_CallsNextMiddleware()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/api/v1/health");

            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync()).ReturnsAsync(settings);

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) => { nextCalled = true; return Task.CompletedTask; };
            var middleware = CreateMiddleware(next);

            // Act
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_WhenAdminUiAndBypassEnabled_CallsNextMiddleware()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/configuration");

            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                BypassForAdminUi = true,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync()).ReturnsAsync(settings);

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) => { nextCalled = true; return Task.CompletedTask; };
            var middleware = CreateMiddleware(next);

            // Act
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_WhenIpAllowed_CallsNextMiddleware()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/api/v1/chat");
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                BypassForAdminUi = false,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync()).ReturnsAsync(settings);
            _mockIpFilterService.Setup(s => s.IsIpAllowedAsync("192.168.1.1")).ReturnsAsync(true);

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) => { nextCalled = true; return Task.CompletedTask; };
            var middleware = CreateMiddleware(next);

            // Act
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert
            Assert.True(nextCalled);
        }

        [Fact]
        public async Task InvokeAsync_WhenIpBlocked_Returns403()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/api/v1/chat");
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
            context.Response.Body = new MemoryStream();

            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                BypassForAdminUi = false,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync()).ReturnsAsync(settings);
            _mockIpFilterService.Setup(s => s.IsIpAllowedAsync("192.168.1.1")).ReturnsAsync(false);

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) => { nextCalled = true; return Task.CompletedTask; };
            var middleware = CreateMiddleware(next);

            // Act
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert
            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WithXForwardedForHeader_UsesFirstIpInHeader()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/api/v1/chat");
            context.Request.Headers["X-Forwarded-For"] = "10.0.0.1, 192.168.1.100, 172.16.0.1";

            var settings = new IpFilterSettingsDto
            {
                IsEnabled = true,
                BypassForAdminUi = false,
                ExcludedEndpoints = new List<string> { "/api/v1/health" }
            };

            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync()).ReturnsAsync(settings);
            _mockIpFilterService.Setup(s => s.IsIpAllowedAsync("10.0.0.1")).ReturnsAsync(true);

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) => { nextCalled = true; return Task.CompletedTask; };
            var middleware = CreateMiddleware(next);

            // Act
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert
            Assert.True(nextCalled);
            _mockIpFilterService.Verify(s => s.IsIpAllowedAsync("10.0.0.1"), Times.Once);
        }

        [Fact]
        public async Task InvokeAsync_WhenIpServiceThrowsException_CallsNextMiddleware()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = new PathString("/api/v1/chat");
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            _mockIpFilterService.Setup(s => s.GetIpFilterSettingsAsync())
                .ThrowsAsync(new Exception("Test exception"));

            bool nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            // Create a new middleware instance with our test delegate
            var middleware = CreateMiddleware(next);

            // Act - the middleware should handle the exception and call next
            await middleware.InvokeAsync(context, _mockIpFilterService.Object);

            // Assert - verify that the next delegate was called
            Assert.True(nextCalled, "Next middleware should be called when an exception occurs");
        }
    }
}
