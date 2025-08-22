using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Setup and helper methods for authentication service tests.
    /// </summary>
    public partial class AuthenticationServiceTests
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