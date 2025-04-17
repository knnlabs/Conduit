using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Data;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure; // Add for DbUpdateException
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

public class GlobalSettingService : IGlobalSettingService
{
    private readonly ConfigurationDbContext _context;
    private readonly ILogger<GlobalSettingService> _logger;
    private const string MasterKeyHashSettingKey = "MasterKeyHash";
    private const string MasterKeyHashAlgorithmSettingKey = "MasterKeyHashAlgorithm";
    private const string DefaultHashAlgorithm = "SHA256"; // Default algorithm

    public GlobalSettingService(ConfigurationDbContext context, ILogger<GlobalSettingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        var setting = await _context.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new GlobalSetting { Key = key, Value = value };
            _context.GlobalSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database update failed while setting key '{Key}'. See inner exception for details.", key);
            // Optionally log Entries causing the error:
            // foreach (var entry in dbEx.Entries) { _logger.LogError("Entity: {EntityName}", entry.Entity.GetType().Name); }
            throw; // Re-throw to allow calling methods to handle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while saving settings for key '{Key}'.", key);
            throw; // Re-throw to allow calling methods to handle
        }
    }

    public Task<string?> GetMasterKeyHashAsync()
    {
        return GetSettingAsync(MasterKeyHashSettingKey);
    }

    public Task<string?> GetMasterKeyHashAlgorithmAsync()
    {
        // Return stored algorithm or default
        return GetSettingAsync(MasterKeyHashAlgorithmSettingKey);
        // Could add logic here: return setting ?? DefaultHashAlgorithm;
    }

    public async Task SetMasterKeyAsync(string masterKey)
    {
        // Hash the master key before storing
        // Use SHA256 by default for now
        string hashAlgorithm = DefaultHashAlgorithm;
        string hashedKey = HashMasterKey(masterKey, hashAlgorithm);

        await SetSettingAsync(MasterKeyHashSettingKey, hashedKey);
        await SetSettingAsync(MasterKeyHashAlgorithmSettingKey, hashAlgorithm);
        _logger.LogInformation("Master key hash has been updated.");
    }

    // Centralized hashing logic - similar to VirtualKeyService but for Master Key
    private string HashMasterKey(string key, string algorithm)
    {
        // Basic implementation, consider enhancing (e.g., salt)
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
            // Add other algorithms here if needed
            _ => SHA256.Create() // Default
        };
    }
}
