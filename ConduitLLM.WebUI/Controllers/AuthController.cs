using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Core.Extensions;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Controller for authentication functions
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IGlobalSettingService _settingService;
        private readonly ILogger<AuthController> _logger;
        private readonly ISecurityService _securityService;

        /// <summary>
        /// Creates a new instance of the AuthController
        /// </summary>
        public AuthController(
            IGlobalSettingService settingService,
            ILogger<AuthController> logger,
            ISecurityService securityService)
        {
            _settingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        /// <summary>
        /// Authenticates a user with the master key
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.MasterKey))
                {
                    return BadRequest(new { message = "Master key is required" });
                }

                // Get client IP
                var clientIp = GetClientIpAddress();

                // Check if IP is banned
                if (await _securityService.IsIpBannedAsync(clientIp))
                {
                    _logger.LogWarning("Login attempt from banned IP: {IpAddress}", clientIp);
                    return StatusCode(429, new { message = "Too many failed login attempts. Please try again later." });
                }

                bool isValid = await ValidateMasterKeyAsync(request.MasterKey);

                if (isValid)
                {
                    // Clear failed login attempts on successful login
                    await _securityService.ClearFailedLoginAttemptsAsync(clientIp);

                    // Create claims for the user
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, "Admin"),
                        new Claim(ClaimTypes.Role, "Administrator"),
                        new Claim("MasterKeyAuthenticated", "true")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "ConduitAuth");
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = request.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(request.RememberMe ? 7 : 1),
                        RedirectUri = request.ReturnUrl ?? "/"
                    };

                    await HttpContext.SignInAsync("ConduitAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

                    return Ok(new { success = true, redirectUrl = request.ReturnUrl ?? "/" });
                }

                // If validation fails, log it and record failed attempt
                _logger.LogWarning("Invalid master key attempt from {IpAddress}", clientIp);
                
                // Record failed login attempt
                await _securityService.RecordFailedLoginAsync(clientIp);
                
                return Unauthorized(new { message = "Invalid master key" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { message = "An error occurred during login" });
            }
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync("ConduitAuth");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { message = "An error occurred during logout" });
            }
        }

        /// <summary>
        /// Gets information about the currently authenticated user
        /// </summary>
        [HttpGet("current-user")]
        public IActionResult GetCurrentUser()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Ok(new
                {
                    isAuthenticated = true,
                    username = User.Identity.Name,
                    roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList()
                });
            }

            return Ok(new { isAuthenticated = false });
        }

        /// <summary>
        /// Gets the client IP address from the request
        /// </summary>
        private string GetClientIpAddress()
        {
            // Check X-Forwarded-For header first (for reverse proxies)
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Take the first IP in the chain
                var ip = forwardedFor.Split(',').First().Trim();
                if (System.Net.IPAddress.TryParse(ip, out _))
                {
                    return ip;
                }
            }

            // Check X-Real-IP header
            var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp) && System.Net.IPAddress.TryParse(realIp, out _))
            {
                return realIp;
            }

            // Fall back to direct connection IP
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>
        /// Validates the master key
        /// </summary>
        private async Task<bool> ValidateMasterKeyAsync(string inputKey)
        {
            if (string.IsNullOrEmpty(inputKey))
                return false;

            try
            {
                // First, check if the input is the exact WebUI auth key from environment
                string? envWebUIKey = Environment.GetEnvironmentVariable("CONDUIT_WEBUI_AUTH_KEY");
                if (!string.IsNullOrEmpty(envWebUIKey) && inputKey.Equals(envWebUIKey, StringComparison.Ordinal))
                {
                    _logger.LogInformation("WebUI auth key validated against environment variable");
                    return true;
                }

                // Fall back to master key for backward compatibility
                string? envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
                if (!string.IsNullOrEmpty(envMasterKey) && inputKey.Equals(envMasterKey, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Master key validated against environment variable (backward compatibility)");
                    return true;
                }

                // Get the stored master key hash and algorithm
                var storedHash = await _settingService.GetMasterKeyHashAsync();
                var storedAlgorithm = await _settingService.GetMasterKeyHashAlgorithmAsync() ?? "SHA256"; // Default to SHA256

                if (string.IsNullOrEmpty(storedHash))
                {
                    _logger.LogWarning("Master key hash is not configured.");
                    return false;
                }

                // Check if the input is the raw hash value (for developer convenience)
                if (inputKey.Equals(storedHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Master key validated against stored hash directly");
                    return true;
                }

                // Hash the provided key using the same algorithm
                string hashedInputKey = HashMasterKey(inputKey, storedAlgorithm);

                // Compare hashes (case-insensitive)
                bool result = string.Equals(hashedInputKey, storedHash, StringComparison.OrdinalIgnoreCase);

                if (result)
                {
                    _logger.LogInformation("Master key validated successfully against hashed value");
                }
                else
                {
                    _logger.LogWarning("Invalid master key attempted");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating master key");
                return false;
            }
        }

        /// <summary>
        /// Hashes the master key using the specified algorithm
        /// </summary>
        private string HashMasterKey(string key, string algorithm)
        {
            // Basic implementation, consider enhancing (e.g., salt)
            using var hasher = GetHashAlgorithmInstance(algorithm);
            var bytes = Encoding.UTF8.GetBytes(key);
            var hashBytes = hasher.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Gets a hash algorithm instance based on the algorithm name
        /// </summary>
        private HashAlgorithm GetHashAlgorithmInstance(string algorithm)
        {
            return algorithm.ToUpperInvariant() switch
            {
                "SHA256" => SHA256.Create(),
                "SHA384" => SHA384.Create(),
                "SHA512" => SHA512.Create(),
                _ => SHA256.Create() // Default to SHA256
            };
        }
    }

    /// <summary>
    /// Represents a login request
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// The master key for authentication
        /// </summary>
        public string MasterKey { get; set; } = string.Empty;

        /// <summary>
        /// Whether to persist the authentication session
        /// </summary>
        public bool RememberMe { get; set; }

        /// <summary>
        /// The URL to redirect to after successful authentication
        /// </summary>
        public string? ReturnUrl { get; set; }
    }
}
