using System.Security.Claims;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.WebUI.Controllers;

/// <summary>
/// Provides authentication endpoints for the Conduit Web UI.
/// </summary>
/// <remarks>
/// This controller handles user authentication using a master key and provides 
/// functionality for logging in, logging out, and checking the current user's authentication status.
/// Authentication is based on a master key that is validated against either an environment variable
/// or a stored hash in the database.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IGlobalSettingService _settingService;
    private readonly ILogger<AuthController> _logger;
    private const string AuthenticationScheme = "ConduitAuth";

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="settingService">The service for accessing global settings, including master key hash.</param>
    /// <param name="logger">The logger for recording diagnostic information.</param>
    public AuthController(
        IGlobalSettingService settingService,
        ILogger<AuthController> logger)
    {
        _settingService = settingService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user using a master key.
    /// </summary>
    /// <param name="request">The login request containing the master key and preferences.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the result of the login operation.
    /// Returns:
    /// - 200 OK with success flag and redirect URL if authentication is successful
    /// - 400 Bad Request if the master key is not provided
    /// - 401 Unauthorized if the master key is invalid
    /// - 500 Internal Server Error if an unexpected error occurs
    /// </returns>
    /// <remarks>
    /// This method validates the provided master key against either an environment variable
    /// or a stored hash in the database. If validation succeeds, it creates an authentication
    /// cookie with administrator privileges.
    /// </remarks>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.MasterKey))
            {
                return BadRequest(new { message = "Master key is required" });
            }

            bool isValid = await ValidateMasterKeyAsync(request.MasterKey);

            if (isValid)
            {
                // Create claims for the user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, "Admin"),
                    new Claim(ClaimTypes.Role, "Administrator"),
                    new Claim("MasterKeyAuthenticated", "true")
                };

                var claimsIdentity = new ClaimsIdentity(claims, AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = request.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(request.RememberMe ? 7 : 1),
                    RedirectUri = request.ReturnUrl ?? "/"
                };

                await HttpContext.SignInAsync(AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                return Ok(new { success = true, redirectUrl = request.ReturnUrl ?? "/" });
            }

            // If validation fails, log it
            _logger.LogWarning("Invalid master key attempt from {IpAddress}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Invalid master key" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Logs the current user out by removing the authentication cookie.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the result of the logout operation.
    /// Returns:
    /// - 200 OK with success flag if logout is successful
    /// - 500 Internal Server Error if an unexpected error occurs
    /// </returns>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await HttpContext.SignOutAsync(AuthenticationScheme);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Gets information about the currently authenticated user.
    /// </summary>
    /// <returns>
    /// An <see cref="IActionResult"/> with information about the current user.
    /// - If authenticated: Returns user name and roles
    /// - If not authenticated: Returns authentication status as false
    /// </returns>
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
    /// Validates the provided master key against configured values.
    /// </summary>
    /// <param name="inputKey">The master key to validate.</param>
    /// <returns>True if the key is valid; otherwise, false.</returns>
    /// <remarks>
    /// This method attempts to validate the provided master key in the following order:
    /// 1. Against the CONDUIT_MASTER_KEY environment variable (exact match)
    /// 2. Against the stored hash directly (for developer convenience)
    /// 3. By hashing the input key and comparing with the stored hash
    /// </remarks>
    private async Task<bool> ValidateMasterKeyAsync(string inputKey)
    {
        if (string.IsNullOrEmpty(inputKey))
            return false;

        try
        {
            // First, check if the input is the exact master key from environment
            string? envMasterKey = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
            if (!string.IsNullOrEmpty(envMasterKey) && inputKey.Equals(envMasterKey, StringComparison.Ordinal))
            {
                _logger.LogInformation("Master key validated against environment variable");
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
    /// Hashes a master key using the specified algorithm.
    /// </summary>
    /// <param name="key">The key to hash.</param>
    /// <param name="algorithm">The hashing algorithm to use.</param>
    /// <returns>The hexadecimal string representation of the hash.</returns>
    private string HashMasterKey(string key, string algorithm)
    {
        // Basic implementation, consider enhancing (e.g., salt)
        using var hasher = GetHashAlgorithmInstance(algorithm);
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hashBytes = hasher.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets an instance of the specified hash algorithm.
    /// </summary>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <returns>A HashAlgorithm instance for the specified algorithm.</returns>
    private System.Security.Cryptography.HashAlgorithm GetHashAlgorithmInstance(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => System.Security.Cryptography.SHA256.Create(),
            // Add other algorithms here if needed
            _ => System.Security.Cryptography.SHA256.Create() // Default
        };
    }
}

/// <summary>
/// Represents a request to log in to the system.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Gets or sets the master key used for authentication.
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets a value indicating whether to persist the authentication cookie.
    /// When true, the authentication cookie will last for 7 days; otherwise, 1 day.
    /// </summary>
    public bool RememberMe { get; set; }
    
    /// <summary>
    /// Gets or sets the URL to redirect to after successful authentication.
    /// If not specified, defaults to the root path ("/").
    /// </summary>
    public string? ReturnUrl { get; set; }
}
