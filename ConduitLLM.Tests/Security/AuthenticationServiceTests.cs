using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for authentication service implementations.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class AuthenticationServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<ISecurityEventMonitoringService> _mockSecurityMonitoring;
        private readonly Mock<ILogger<IAuthenticationService>> _mockLogger;
        private readonly ITestOutputHelper _output;

        public AuthenticationServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockConfiguration = new Mock<IConfiguration>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            _mockSecurityMonitoring = new Mock<ISecurityEventMonitoringService>();
            _mockLogger = new Mock<ILogger<IAuthenticationService>>();
            
            SetupConfiguration();
        }

        private void SetupConfiguration()
        {
            _mockConfiguration.Setup(x => x["Security:ApiKeyHeader"]).Returns("X-API-Key");
            _mockConfiguration.Setup(x => x["Security:RequireHttps"]).Returns("true");
            _mockConfiguration.Setup(x => x["Security:EnableApiKeyAuth"]).Returns("true");
            _mockConfiguration.Setup(x => x["Security:AdminPassword"]).Returns("admin123!");
        }

        #region AuthenticateAsync Tests

        [Fact]
        public async Task AuthenticateAsync_WithValidApiKey_ShouldReturnSuccess()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("valid-api-key", "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            service.AddValidApiKey("valid-api-key", "user123");

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeTrue();
            result.UserId.Should().Be("user123");
            result.AuthenticationMethod.Should().Be("ApiKey");
        }

        [Fact]
        public async Task AuthenticateAsync_WithInvalidApiKey_ShouldReturnFailure()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("invalid-key", "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("Invalid API key");
            
            // Should record security event
            _mockSecurityMonitoring.Verify(x => x.RecordEventAsync(
                It.Is<SecurityEvent>(e => 
                    e.EventType == SecurityEventType.FailedAuthentication &&
                    e.SourceIp == "192.168.1.100")),
                Times.Once);
        }

        [Fact]
        public async Task AuthenticateAsync_WithMissingApiKey_ShouldReturnFailure()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext(null, "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("API key not provided");
        }

        [Fact]
        public async Task AuthenticateAsync_WithDisabledApiKeyAuth_ShouldAllowAnonymous()
        {
            // Arrange
            _mockConfiguration.Setup(x => x["Security:EnableApiKeyAuth"]).Returns("false");
            
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext(null, "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeTrue();
            result.UserId.Should().Be("anonymous");
            result.AuthenticationMethod.Should().Be("Anonymous");
        }

        #endregion

        #region ValidateApiKey Tests

        [Fact]
        public async Task ValidateApiKey_WithValidKey_ShouldReturnUser()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            service.AddValidApiKey("test-key-123", "user456");

            // Act
            var result = await service.ValidateApiKeyAsync("test-key-123");

            // Assert
            result.IsValid.Should().BeTrue();
            result.UserId.Should().Be("user456");
            result.Permissions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValidateApiKey_WithExpiredKey_ShouldReturnInvalid()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            service.AddValidApiKey("expired-key", "user789", DateTime.UtcNow.AddDays(-1));

            // Act
            var result = await service.ValidateApiKeyAsync("expired-key");

            // Assert
            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().Be("API key expired");
        }

        [Fact]
        public async Task ValidateApiKey_WithRevokedKey_ShouldReturnInvalid()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            service.AddValidApiKey("revoked-key", "user999", isRevoked: true);

            // Act
            var result = await service.ValidateApiKeyAsync("revoked-key");

            // Assert
            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().Be("API key revoked");
        }

        #endregion

        #region Admin Authentication Tests

        [Fact]
        public async Task AuthenticateAdmin_WithCorrectPassword_ShouldSucceed()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);

            // Act
            var result = await service.AuthenticateAdminAsync("admin", "admin123!");

            // Assert
            result.IsAuthenticated.Should().BeTrue();
            result.UserId.Should().Be("admin");
            result.AuthenticationMethod.Should().Be("Password");
        }

        [Fact]
        public async Task AuthenticateAdmin_WithIncorrectPassword_ShouldFail()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext(null, "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Act
            var result = await service.AuthenticateAdminAsync("admin", "wrong-password");

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("Invalid credentials");
            
            // Should record security event
            _mockSecurityMonitoring.Verify(x => x.RecordEventAsync(
                It.Is<SecurityEvent>(e => 
                    e.EventType == SecurityEventType.FailedAuthentication &&
                    e.Details!.Contains("Admin login failed"))),
                Times.Once);
        }

        #endregion

        #region Token Generation Tests

        [Fact]
        public async Task GenerateToken_WithValidUser_ShouldReturnToken()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);

            // Act
            var token = await service.GenerateTokenAsync("user123", new[] { "read", "write" });

            // Assert
            token.Should().NotBeNullOrWhiteSpace();
            token.Should().HaveLength(32); // Mock implementation returns 32-char token
        }

        [Fact]
        public async Task ValidateToken_WithValidToken_ShouldReturnClaims()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var token = await service.GenerateTokenAsync("user123", new[] { "read" });

            // Act
            var result = await service.ValidateTokenAsync(token);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "user123");
            result.Claims.Should().Contain(c => c.Type == "permission" && c.Value == "read");
        }

        [Fact]
        public async Task ValidateToken_WithInvalidToken_ShouldReturnInvalid()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);

            // Act
            var result = await service.ValidateTokenAsync("invalid-token");

            // Assert
            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().Be("Invalid token");
        }

        #endregion

        #region HTTPS Requirement Tests

        [Fact]
        public async Task AuthenticateAsync_WithHttpWhenHttpsRequired_ShouldFail()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("valid-key", "192.168.1.100", isHttps: false);
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);
            
            service.AddValidApiKey("valid-key", "user123");

            // Act
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("HTTPS required");
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public async Task AuthenticateAsync_WithTooManyFailedAttempts_ShouldBlockTemporarily()
        {
            // Arrange
            var service = new MockAuthenticationService(
                _mockConfiguration.Object,
                _mockHttpContextAccessor.Object,
                _mockSecurityMonitoring.Object,
                _mockLogger.Object);
            
            var context = CreateHttpContext("invalid-key", "192.168.1.100");
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            // Simulate multiple failed attempts
            for (int i = 0; i < 5; i++)
            {
                await service.AuthenticateAsync();
            }

            // Act - 6th attempt should be blocked
            var result = await service.AuthenticateAsync();

            // Assert
            result.IsAuthenticated.Should().BeFalse();
            result.FailureReason.Should().Be("Too many failed attempts. Try again later.");
        }

        #endregion

        #region Helper Methods

        private HttpContext CreateHttpContext(string? apiKey, string ipAddress, bool isHttps = true)
        {
            var context = new DefaultHttpContext();
            
            if (apiKey != null)
            {
                context.Request.Headers["X-API-Key"] = apiKey;
            }
            
            context.Request.Scheme = isHttps ? "https" : "http";
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);
            
            return context;
        }

        #endregion
    }

    // Interfaces and models for testing
    public interface IAuthenticationService
    {
        Task<AuthenticationResult> AuthenticateAsync();
        Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey);
        Task<AuthenticationResult> AuthenticateAdminAsync(string username, string password);
        Task<string> GenerateTokenAsync(string userId, IEnumerable<string> permissions);
        Task<TokenValidationResult> ValidateTokenAsync(string token);
    }

    public interface ISecurityEventMonitoringService
    {
        Task RecordEventAsync(SecurityEvent securityEvent);
    }

    public class AuthenticationResult
    {
        public bool IsAuthenticated { get; set; }
        public string? UserId { get; set; }
        public string? AuthenticationMethod { get; set; }
        public string? FailureReason { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public class ApiKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string? UserId { get; set; }
        public List<string> Permissions { get; set; } = new();
        public string? FailureReason { get; set; }
    }

    public class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public List<Claim> Claims { get; set; } = new();
        public string? FailureReason { get; set; }
    }

    // Mock implementation
    public class MockAuthenticationService : IAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISecurityEventMonitoringService _securityMonitoring;
        private readonly ILogger<IAuthenticationService> _logger;
        
        private readonly Dictionary<string, ApiKeyInfo> _apiKeys = new();
        private readonly Dictionary<string, TokenInfo> _tokens = new();
        private readonly Dictionary<string, int> _failedAttempts = new();

        public MockAuthenticationService(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ISecurityEventMonitoringService securityMonitoring,
            ILogger<IAuthenticationService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _securityMonitoring = securityMonitoring ?? throw new ArgumentNullException(nameof(securityMonitoring));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void AddValidApiKey(string apiKey, string userId, DateTime? expiresAt = null, bool isRevoked = false)
        {
            _apiKeys[apiKey] = new ApiKeyInfo
            {
                UserId = userId,
                ExpiresAt = expiresAt,
                IsRevoked = isRevoked,
                Permissions = new List<string> { "read", "write" }
            };
        }

        public async Task<AuthenticationResult> AuthenticateAsync()
        {
            await Task.Delay(1);
            
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return new AuthenticationResult { IsAuthenticated = false, FailureReason = "No HTTP context" };
            }

            var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Check HTTPS requirement
            if (_configuration["Security:RequireHttps"] == "true" && context.Request.Scheme != "https")
            {
                return new AuthenticationResult { IsAuthenticated = false, FailureReason = "HTTPS required" };
            }

            // Check rate limiting
            if (_failedAttempts.GetValueOrDefault(sourceIp, 0) >= 5)
            {
                return new AuthenticationResult { IsAuthenticated = false, FailureReason = "Too many failed attempts. Try again later." };
            }

            // Check if API key auth is enabled
            if (_configuration["Security:EnableApiKeyAuth"] != "true")
            {
                return new AuthenticationResult 
                { 
                    IsAuthenticated = true, 
                    UserId = "anonymous",
                    AuthenticationMethod = "Anonymous"
                };
            }

            // Get API key from header
            var apiKeyHeader = _configuration["Security:ApiKeyHeader"] ?? "X-API-Key";
            var apiKey = context.Request.Headers[apiKeyHeader].ToString();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                RecordFailedAttempt(sourceIp);
                return new AuthenticationResult { IsAuthenticated = false, FailureReason = "API key not provided" };
            }

            // Validate API key
            var validation = await ValidateApiKeyAsync(apiKey);
            if (!validation.IsValid)
            {
                RecordFailedAttempt(sourceIp);
                await _securityMonitoring.RecordEventAsync(new SecurityEvent
                {
                    EventType = SecurityEventType.FailedAuthentication,
                    SourceIp = sourceIp,
                    Details = $"Invalid API key: {validation.FailureReason}"
                });
                
                return new AuthenticationResult 
                { 
                    IsAuthenticated = false, 
                    FailureReason = validation.FailureReason ?? "Invalid API key"
                };
            }

            // Success
            _failedAttempts.Remove(sourceIp);
            await _securityMonitoring.RecordEventAsync(new SecurityEvent
            {
                EventType = SecurityEventType.SuccessfulAuthentication,
                SourceIp = sourceIp,
                UserId = validation.UserId
            });

            return new AuthenticationResult
            {
                IsAuthenticated = true,
                UserId = validation.UserId,
                AuthenticationMethod = "ApiKey"
            };
        }

        public async Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey)
        {
            await Task.Delay(1);

            if (!_apiKeys.TryGetValue(apiKey, out var keyInfo))
            {
                return new ApiKeyValidationResult { IsValid = false, FailureReason = "Invalid API key" };
            }

            if (keyInfo.IsRevoked)
            {
                return new ApiKeyValidationResult { IsValid = false, FailureReason = "API key revoked" };
            }

            if (keyInfo.ExpiresAt.HasValue && keyInfo.ExpiresAt.Value < DateTime.UtcNow)
            {
                return new ApiKeyValidationResult { IsValid = false, FailureReason = "API key expired" };
            }

            return new ApiKeyValidationResult
            {
                IsValid = true,
                UserId = keyInfo.UserId,
                Permissions = keyInfo.Permissions
            };
        }

        public async Task<AuthenticationResult> AuthenticateAdminAsync(string username, string password)
        {
            await Task.Delay(1);

            var correctPassword = _configuration["Security:AdminPassword"];
            if (username == "admin" && password == correctPassword)
            {
                return new AuthenticationResult
                {
                    IsAuthenticated = true,
                    UserId = "admin",
                    AuthenticationMethod = "Password"
                };
            }

            var context = _httpContextAccessor.HttpContext;
            var sourceIp = context?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            await _securityMonitoring.RecordEventAsync(new SecurityEvent
            {
                EventType = SecurityEventType.FailedAuthentication,
                SourceIp = sourceIp,
                Details = "Admin login failed"
            });

            return new AuthenticationResult
            {
                IsAuthenticated = false,
                FailureReason = "Invalid credentials"
            };
        }

        public async Task<string> GenerateTokenAsync(string userId, IEnumerable<string> permissions)
        {
            await Task.Delay(1);

            var token = Guid.NewGuid().ToString("N"); // 32-char token
            _tokens[token] = new TokenInfo
            {
                UserId = userId,
                Permissions = permissions.ToList(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            return token;
        }

        public async Task<TokenValidationResult> ValidateTokenAsync(string token)
        {
            await Task.Delay(1);

            if (!_tokens.TryGetValue(token, out var tokenInfo))
            {
                return new TokenValidationResult { IsValid = false, FailureReason = "Invalid token" };
            }

            if (tokenInfo.ExpiresAt < DateTime.UtcNow)
            {
                return new TokenValidationResult { IsValid = false, FailureReason = "Token expired" };
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, tokenInfo.UserId),
                new Claim("token_created", tokenInfo.CreatedAt.ToString("O"))
            };

            foreach (var permission in tokenInfo.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            return new TokenValidationResult
            {
                IsValid = true,
                Claims = claims
            };
        }

        private void RecordFailedAttempt(string sourceIp)
        {
            _failedAttempts[sourceIp] = _failedAttempts.GetValueOrDefault(sourceIp, 0) + 1;
            _logger.LogWarning("Failed authentication attempt from {SourceIp}", sourceIp);
        }

        private class ApiKeyInfo
        {
            public string UserId { get; set; } = string.Empty;
            public DateTime? ExpiresAt { get; set; }
            public bool IsRevoked { get; set; }
            public List<string> Permissions { get; set; } = new();
        }

        private class TokenInfo
        {
            public string UserId { get; set; } = string.Empty;
            public List<string> Permissions { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}