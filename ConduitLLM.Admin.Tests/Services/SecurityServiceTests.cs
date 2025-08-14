using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models;
using ConduitLLM.Admin.Options;
using ConduitLLM.Admin.Services;

namespace ConduitLLM.Admin.Tests.Services
{
    public class SecurityServiceTests
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<SecurityService>> _loggerMock;
        private readonly Mock<IMemoryCache> _memoryCacheMock;
        private readonly Mock<IDistributedCache> _distributedCacheMock;
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock;
        private readonly IOptions<SecurityOptions> _securityOptions;
        private readonly SecurityService _securityService;

        public SecurityServiceTests()
        {
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<SecurityService>>();
            _memoryCacheMock = new Mock<IMemoryCache>();
            _distributedCacheMock = new Mock<IDistributedCache>();
            _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();

            var securityOptions = new SecurityOptions
            {
                ApiAuth = new ApiAuthOptions
                {
                    ApiKeyHeader = "X-API-Key",
                    AlternativeHeaders = new List<string> { "X-Master-Key" }
                },
                RateLimiting = new RateLimitingOptions { Enabled = false },
                IpFiltering = new IpFilteringOptions { Enabled = false },
                FailedAuth = new FailedAuthOptions { Enabled = false }
            };
            _securityOptions = Microsoft.Extensions.Options.Options.Create(securityOptions);

            _securityService = new SecurityService(
                _securityOptions,
                _configurationMock.Object,
                _loggerMock.Object,
                _memoryCacheMock.Object,
                _distributedCacheMock.Object,
                _serviceScopeFactoryMock.Object
            );
        }

        [Fact]
        public async Task IsRequestAllowedAsync_WithValidMasterKey_ReturnsAllowed()
        {
            // Arrange
            var masterKey = "test-master-key";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", masterKey);

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            context.Request.Headers["X-API-Key"] = masterKey;

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal("", result.Reason);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public async Task IsRequestAllowedAsync_WithEphemeralMasterKey_ReturnsAllowed()
        {
            // Arrange
            var ephemeralKey = "emk_testkey123456789";
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            context.Request.Headers["X-API-Key"] = ephemeralKey;

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal("", result.Reason);
        }

        [Fact]
        public async Task IsRequestAllowedAsync_WithEphemeralKeyInAlternativeHeader_ReturnsAllowed()
        {
            // Arrange
            var ephemeralKey = "emk_testkey123456789";
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            context.Request.Headers["X-Master-Key"] = ephemeralKey;

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal("", result.Reason);
        }

        [Fact]
        public async Task IsRequestAllowedAsync_WithInvalidKey_ReturnsNotAllowed()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", "correct-key");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            context.Request.Headers["X-API-Key"] = "wrong-key";

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal("Invalid or missing API key", result.Reason);
            Assert.Equal(401, result.StatusCode);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public async Task IsRequestAllowedAsync_WithNoKey_ReturnsNotAllowed()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal("Invalid or missing API key", result.Reason);
            Assert.Equal(401, result.StatusCode);
        }

        [Fact]
        public async Task IsRequestAllowedAsync_ExcludedPath_ReturnsAllowed()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/health";
            // No API key provided

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal("", result.Reason);
        }

        [Fact]
        public void ValidateApiKey_WithCorrectMasterKey_ReturnsTrue()
        {
            // Arrange
            var masterKey = "test-master-key";
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", masterKey);

            // Act
            var result = _securityService.ValidateApiKey(masterKey);

            // Assert
            Assert.True(result);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Fact]
        public void ValidateApiKey_WithIncorrectKey_ReturnsFalse()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", "correct-key");

            // Act
            var result = _securityService.ValidateApiKey("wrong-key");

            // Assert
            Assert.False(result);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }

        [Theory]
        [InlineData("emk_abc123")]
        [InlineData("emk_xyz789")]
        [InlineData("emk_1234567890abcdef")]
        public async Task IsRequestAllowedAsync_VariousEphemeralKeyFormats_ReturnsAllowed(string ephemeralKey)
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            context.Request.Headers["X-API-Key"] = ephemeralKey;

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal("", result.Reason);
        }

        [Theory]
        [InlineData("em_notvalid")]
        [InlineData("EMK_uppercase")]
        [InlineData("notakey")]
        [InlineData("")]
        public async Task IsRequestAllowedAsync_InvalidKeyFormats_ReturnsNotAllowed(string invalidKey)
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", "master-key");

            var context = new DefaultHttpContext();
            context.Request.Path = "/api/test";
            context.Request.Headers["X-API-Key"] = invalidKey;

            // Act
            var result = await _securityService.IsRequestAllowedAsync(context);

            // Assert
            Assert.False(result.IsAllowed);

            // Cleanup
            Environment.SetEnvironmentVariable("CONDUIT_API_TO_API_BACKEND_AUTH_KEY", null);
        }
    }
}