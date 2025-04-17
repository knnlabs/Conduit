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

public class MasterKeyAuthorizationHandler : AuthorizationHandler<MasterKeyRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider; // To resolve scoped service
    private readonly ILogger<MasterKeyAuthorizationHandler> _logger;
    private const string MasterKeyHeaderName = "X-Master-Key";

    public MasterKeyAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider, 
        ILogger<MasterKeyAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider; 
        _logger = logger;
    }

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

        // Retrieve the master key from the request header
        if (!httpContext.Request.Headers.TryGetValue(MasterKeyHeaderName, out var providedMasterKeyValues) ||
            string.IsNullOrWhiteSpace(providedMasterKeyValues.FirstOrDefault()))
        {
            _logger.LogDebug("Master key header '{HeaderName}' not found or empty.", MasterKeyHeaderName);
            context.Fail();
            return;
        }

        var providedMasterKey = providedMasterKeyValues.First()!;

        // Resolve the GlobalSettingService within the request scope
        // This is needed because AuthorizationHandlers are singletons but DbContext is scoped
        using var scope = _serviceProvider.CreateScope();
        var settingService = scope.ServiceProvider.GetRequiredService<IGlobalSettingService>();

        // Retrieve stored hash and algorithm
        var storedHash = await settingService.GetMasterKeyHashAsync();
        var storedAlgorithm = await settingService.GetMasterKeyHashAlgorithmAsync() ?? "SHA256"; // Use default if not set

        if (string.IsNullOrWhiteSpace(storedHash))
        {
            _logger.LogWarning("Master key hash is not configured in settings.");
            context.Fail(new AuthorizationFailureReason(this, "Master key not configured."));
            return;
        }

        // Hash the provided key
        string providedKeyHash;
        try
        {
            providedKeyHash = HashMasterKey(providedMasterKey, storedAlgorithm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hashing provided master key using algorithm {Algorithm}.", storedAlgorithm);
            context.Fail();
            return;
        }

        // Compare hashes (case-insensitive)
        if (string.Equals(providedKeyHash, storedHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Master key validated successfully.");
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Provided master key failed validation.");
            context.Fail(new AuthorizationFailureReason(this, "Invalid master key."));
        }
    }

    // Consistent hashing logic (could be shared/refactored)
    private string HashMasterKey(string key, string algorithm)
    {
        using var hasher = GetHashAlgorithmInstance(algorithm);
        var bytes = Encoding.UTF8.GetBytes(key);
        var hashBytes = hasher.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private HashAlgorithm GetHashAlgorithmInstance(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => SHA256.Create(),
            _ => throw new NotSupportedException($"Hash algorithm '{algorithm}' is not supported.")
        };
    }
}
