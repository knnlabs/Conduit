using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Authorization;

/// <summary>
/// Authorization handler that validates requests against a configured master key.
/// </summary>
/// <remarks>
/// <para>
/// This handler implements the verification logic for the <see cref="MasterKeyRequirement"/>
/// authorization requirement. It validates that the request includes a valid master key
/// in the X-Master-Key header. The master key is validated by comparing a hash of the
/// provided key with a stored hash retrieved from the global settings.
/// </para>
/// <para>
/// The master key authentication is optional if no master key hash is configured in
/// the system. In this case, all requests will be authorized regardless of whether
/// they include a master key.
/// </para>
/// <para>
/// When a master key is configured, requests must include a valid master key in the
/// X-Master-Key header to be authorized.
/// </para>
/// </remarks>
public class MasterKeyAuthorizationHandler : AuthorizationHandler<MasterKeyRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider; // To resolve scoped service
    private readonly ILogger<MasterKeyAuthorizationHandler> _logger;
    private const string MasterKeyHeaderName = "X-Master-Key";

    /// <summary>
    /// Initializes a new instance of the <see cref="MasterKeyAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="serviceProvider">The service provider for resolving scoped services.</param>
    /// <param name="logger">The logger for recording diagnostic information.</param>
    public MasterKeyAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider, 
        ILogger<MasterKeyAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider; 
        _logger = logger;
    }

    /// <summary>
    /// Handles the authorization requirement by validating the master key.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The authorization requirement.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item>Retrieves the stored master key hash from the global settings</item>
    /// <item>If no master key is configured, all requests are authorized</item>
    /// <item>If a master key is configured, validates the provided master key in the X-Master-Key header</item>
    /// <item>Authorizes the request if the hashed provided key matches the stored hash</item>
    /// </list>
    /// </remarks>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MasterKeyRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null in MasterKeyAuthorizationHandler.");
            context.Fail();
            return;
        }

        // Resolve the GlobalSettingService within the request scope
        // This is needed because AuthorizationHandlers are singletons but DbContext is scoped
        using var scope = _serviceProvider.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<IGlobalSettingService>();

        // Retrieve stored hash and algorithm
        var storedHash = await settingService.GetMasterKeyHashAsync();
        var storedAlgorithm = await settingService.GetMasterKeyHashAlgorithmAsync() ?? "SHA256"; // Use default if not set

        // If no master key is configured in the system settings, allow access.
        // Authentication becomes effectively optional.
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            _logger.LogInformation("No master key configured system-wide. Allowing access without master key authentication.");
            context.Succeed(requirement); // Succeed if no key is set up
            return;
        }

        // If we reach here, a master key IS configured. The header is now mandatory.
        // Retrieve the master key from the request header
        if (!httpContext.Request.Headers.TryGetValue(MasterKeyHeaderName, out var providedMasterKeyValues) ||
            string.IsNullOrWhiteSpace(providedMasterKeyValues.FirstOrDefault()))
        {
            _logger.LogWarning("Master key is configured, but header '{HeaderName}' was not provided or was empty.", MasterKeyHeaderName);
            // Fail explicitly stating the header is missing now that we know a key is configured
            context.Fail(new AuthorizationFailureReason(this, $"Master key required but header '{MasterKeyHeaderName}' not provided."));
            return;
        }

        var providedMasterKey = providedMasterKeyValues.First()!;

        // Hash the provided key
        string providedKeyHash;
        try
        {
            providedKeyHash = HashMasterKey(providedMasterKey, storedAlgorithm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hashing provided master key using algorithm {Algorithm}.", storedAlgorithm);
            context.Fail(); // Fail on hashing error
            return;
        }

        // Compare hashes (case-insensitive)
        if (string.Equals(providedKeyHash, storedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Master key validated successfully.");
            context.Succeed(requirement); // Succeed on match
        }
        else
        {
            _logger.LogWarning("Provided master key failed validation.");
            context.Fail(new AuthorizationFailureReason(this, "Invalid master key.")); // Fail on mismatch
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
        using var hasher = GetHashAlgorithmInstance(algorithm);
        var bytes = Encoding.UTF8.GetBytes(key);
        var hashBytes = hasher.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets an instance of the specified hash algorithm.
    /// </summary>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <returns>A <see cref="HashAlgorithm"/> instance for the specified algorithm.</returns>
    /// <exception cref="NotSupportedException">Thrown when the specified algorithm is not supported.</exception>
    private HashAlgorithm GetHashAlgorithmInstance(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => SHA256.Create(),
            _ => throw new NotSupportedException($"Hash algorithm '{algorithm}' is not supported.")
        };
    }
}
