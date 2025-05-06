using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for managing global application settings stored in the database.
/// </summary>
/// <remarks>
/// <para>
/// This service provides a unified interface for accessing and modifying global
/// application settings. It uses the ConfigurationDbContext to store settings
/// as key-value pairs in the database.
/// </para>
/// <para>
/// It includes special handling for security-sensitive settings like the master key,
/// which is never stored in plaintext but rather as a cryptographic hash.
/// </para>
/// </remarks>
public class GlobalSettingService : IGlobalSettingService
{
    private readonly IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> _configContextFactory; 
    private readonly ILogger<GlobalSettingService> _logger;
    
    /// <summary>
    /// Key for storing the master key hash in the settings database.
    /// </summary>
    private const string MasterKeyHashSettingKey = "MasterKeyHash";
    
    /// <summary>
    /// Key for storing the algorithm used to hash the master key.
    /// </summary>
    private const string MasterKeyHashAlgorithmSettingKey = "MasterKeyHashAlgorithm";
    
    /// <summary>
    /// Default hashing algorithm to use for master keys.
    /// </summary>
    private const string DefaultHashAlgorithm = "SHA256";

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalSettingService"/> class.
    /// </summary>
    /// <param name="configContextFactory">Factory for creating database context instances.</param>
    /// <param name="logger">Logger for recording diagnostic information.</param>
    public GlobalSettingService(
        IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configContextFactory, 
        ILogger<GlobalSettingService> logger)
    {
        _configContextFactory = configContextFactory ?? throw new ArgumentNullException(nameof(configContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string?> GetSettingAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));
        }
        
        try
        {
            using var context = await _configContextFactory.CreateDbContextAsync();
            
            if (context == null)
            {
                _logger.LogError("Database context is null in GetSettingAsync");
                return null;
            }
            
            if (context.GlobalSettings == null)
            {
                _logger.LogError("GlobalSettings DbSet is null in database context");
                return null;
            }
            
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
            return setting?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting setting for key '{Key}'", key);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetSettingAsync(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Setting key cannot be null or empty", nameof(key));
        }
        
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "Setting value cannot be null");
        }
        
        try
        {
            using var context = await _configContextFactory.CreateDbContextAsync();
            
            if (context == null)
            {
                _logger.LogError("Database context is null in SetSettingAsync");
                return;
            }
            
            if (context.GlobalSettings == null)
            {
                _logger.LogError("GlobalSettings DbSet is null in database context");
                return;
            }
            
            var setting = await context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
            
            if (setting == null)
            {
                // Create a new setting
                setting = new ConduitLLM.Configuration.Entities.GlobalSetting { Key = key, Value = value }; 
                context.GlobalSettings.Add(setting);
                _logger.LogDebug("Creating new setting with key '{Key}'", key);
            }
            else
            {
                // Update existing setting
                setting.Value = value;
                _logger.LogDebug("Updating existing setting with key '{Key}'", key);
            }
            
            await context.SaveChangesAsync();
            _logger.LogDebug("Successfully saved setting with key '{Key}'", key);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database update failed while setting key '{Key}'. See inner exception for details.", key);
            // We could log the specific entities causing the issue:
            // foreach (var entry in dbEx.Entries) { _logger.LogError("Entity: {EntityName}", entry.Entity.GetType().Name); }
            throw; // Re-throw to allow calling methods to handle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while saving settings for key '{Key}'.", key);
            throw; // Re-throw to allow calling methods to handle
        }
    }

    /// <inheritdoc/>
    public Task<string?> GetMasterKeyHashAsync()
    {
        _logger.LogDebug("Retrieving master key hash");
        return GetSettingAsync(MasterKeyHashSettingKey);
    }

    /// <inheritdoc/>
    public async Task<string?> GetMasterKeyHashAlgorithmAsync()
    {
        _logger.LogDebug("Retrieving master key hash algorithm");
        var algorithm = await GetSettingAsync(MasterKeyHashAlgorithmSettingKey);
        
        // If no algorithm is stored, return the default algorithm
        if (string.IsNullOrEmpty(algorithm))
        {
            _logger.LogDebug("No hash algorithm found in settings, returning default: {DefaultAlgorithm}", DefaultHashAlgorithm);
            return DefaultHashAlgorithm;
        }
        
        return algorithm;
    }

    /// <inheritdoc/>
    public async Task SetMasterKeyAsync(string masterKey)
    {
        if (string.IsNullOrEmpty(masterKey))
        {
            throw new ArgumentException("Master key cannot be null or empty", nameof(masterKey));
        }
        
        // Use the default hashing algorithm
        string hashAlgorithm = DefaultHashAlgorithm;
        
        // Hash the master key
        string hashedKey = HashMasterKey(masterKey, hashAlgorithm);
        
        // Store both the hash and the algorithm used
        await SetSettingAsync(MasterKeyHashSettingKey, hashedKey);
        await SetSettingAsync(MasterKeyHashAlgorithmSettingKey, hashAlgorithm);
        
        _logger.LogInformation("Master key hash has been updated");
    }

    /// <summary>
    /// Hashes a master key using the specified algorithm.
    /// </summary>
    /// <param name="key">The raw master key to hash.</param>
    /// <param name="algorithm">The hashing algorithm to use.</param>
    /// <returns>A hexadecimal string representation of the hash.</returns>
    /// <remarks>
    /// This is a simple implementation that could be enhanced with salt, 
    /// key stretching, or other security features in the future.
    /// </remarks>
    private string HashMasterKey(string key, string algorithm)
    {
        using var hasher = GetHashAlgorithmInstance(algorithm);
        var bytes = Encoding.UTF8.GetBytes(key);
        var hashBytes = hasher.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Gets an instance of the specified hashing algorithm.
    /// </summary>
    /// <param name="algorithm">The name of the algorithm.</param>
    /// <returns>An instance of the hashing algorithm.</returns>
    private HashAlgorithm GetHashAlgorithmInstance(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            // Add other algorithms as needed
            _ => SHA256.Create() // Default to SHA256
        };
    }
}
