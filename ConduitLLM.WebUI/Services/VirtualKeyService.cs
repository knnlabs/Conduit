using System.Security.Cryptography;
using System.Text;

using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

public class VirtualKeyService : IVirtualKeyService
{
    // Inject the factory for the CORRECT DbContext that manages VirtualKeys
    private readonly IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> _configContextFactory; 
    private readonly ILogger<VirtualKeyService> _logger;
    private const int KeyLengthBytes = 32; // Generate a 256-bit key

    // Update constructor signature
    public VirtualKeyService(IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configContextFactory, ILogger<VirtualKeyService> logger)
    {
        _configContextFactory = configContextFactory; // Assign the correct factory
        _logger = logger;
    }

    /// <summary>
    /// Generates a new virtual key and saves its hash to the database.
    /// </summary>
    public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
    {
        var newKey = GenerateNewKeyString();
        var keyHash = HashKey(newKey);

        var virtualKeyEntity = new ConduitLLM.Configuration.Entities.VirtualKey
        {
            KeyName = request.KeyName,
            KeyHash = keyHash,
            AllowedModels = request.AllowedModels,
            MaxBudget = request.MaxBudget,
            BudgetDuration = request.BudgetDuration,
            BudgetStartDate = DetermineBudgetStartDate(request.BudgetDuration),
            IsEnabled = true,
            ExpiresAt = request.ExpiresAt?.ToUniversalTime(),
            Metadata = request.Metadata,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RateLimitRpm = request.RateLimitRpm,
            RateLimitRpd = request.RateLimitRpd
        };

        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        context.VirtualKeys.Add(virtualKeyEntity);
        await context.SaveChangesAsync();

        var keyDto = MapToDto(virtualKeyEntity);
        // Note: MapToDto doesn't have access to the raw key for prefix generation here.
        // We might need a different approach if prefix is needed outside Generate.

        return new CreateVirtualKeyResponseDto
        {
            VirtualKey = newKey, // Return the actual key only upon creation
            KeyInfo = keyDto
        };
    }

    /// <summary>
    /// Retrieves information about a specific virtual key.
    /// </summary>
    /// <param name="id">The ID of the key.</param>
    /// <returns>A DTO with key details, or null if not found.</returns>
    public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = await context.VirtualKeys
                                         .AsNoTracking() // Read-only operation
                                         .FirstOrDefaultAsync(vk => vk.Id == id);

        return virtualKey == null ? null : MapToDto(virtualKey);
    }

    /// <summary>
    /// Retrieves a list of all virtual keys.
    /// </summary>
    /// <returns>A list of DTOs representing the keys.</returns>
    public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        List<ConduitLLM.Configuration.Entities.VirtualKey> virtualKeys = await context.VirtualKeys
                                          .AsNoTracking()
                                          .OrderBy(vk => vk.KeyName)
                                          .ToListAsync();

        return virtualKeys.Select((ConduitLLM.Configuration.Entities.VirtualKey vk) => MapToDto(vk)).ToList();
    }

    /// <summary>
    /// Updates an existing virtual key.
    /// </summary>
    /// <param name="id">The ID of the key to update.</param>
    /// <param name="request">The update request details.</param>
    /// <returns>True if update was successful, false if key not found.</returns>
    public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        var virtualKey = await context.VirtualKeys.FindAsync(id);
        if (virtualKey == null) return false;
        virtualKey.KeyName = request.KeyName ?? virtualKey.KeyName;
        virtualKey.AllowedModels = request.AllowedModels ?? virtualKey.AllowedModels;
        virtualKey.MaxBudget = request.MaxBudget ?? virtualKey.MaxBudget;
        virtualKey.BudgetDuration = request.BudgetDuration ?? virtualKey.BudgetDuration;
        if (request.BudgetDuration != null)
            virtualKey.BudgetStartDate = DetermineBudgetStartDate(request.BudgetDuration);
        virtualKey.IsEnabled = request.IsEnabled ?? virtualKey.IsEnabled;
        virtualKey.ExpiresAt = request.ExpiresAt?.ToUniversalTime() ?? virtualKey.ExpiresAt;
        if (request.Metadata != null)
            virtualKey.Metadata = string.IsNullOrEmpty(request.Metadata) ? null : request.Metadata;
        virtualKey.UpdatedAt = DateTime.UtcNow;
        virtualKey.RateLimitRpm = request.RateLimitRpm;
        virtualKey.RateLimitRpd = request.RateLimitRpd;
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Deletes a virtual key by its ID.
    /// </summary>
    /// <param name="id">The ID of the key to delete.</param>
    /// <returns>True if deletion was successful, false if key not found.</returns>
    public async Task<bool> DeleteVirtualKeyAsync(int id)
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = await context.VirtualKeys.FindAsync(id);
        
        if (virtualKey == null)
        {
            return false; // Key not found
        }

        context.VirtualKeys.Remove(virtualKey);
        
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting virtual key with ID {KeyId}.", id);
            return false;
        }
    }

    /// <summary>
    /// Resets the current spend for a virtual key and potentially resets the budget start date.
    /// </summary>
    /// <param name="id">The ID of the key to reset spend for.</param>
    /// <returns>True if reset was successful, false if key not found.</returns>
    public async Task<bool> ResetSpendAsync(int id)
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = await context.VirtualKeys.FindAsync(id);

        if (virtualKey == null)
        {
            return false; // Key not found
        }

        // Reset spend to zero
        virtualKey.CurrentSpend = 0;

        // Reset budget start date based on current budget duration
        if (!string.IsNullOrEmpty(virtualKey.BudgetDuration) &&
            !virtualKey.BudgetDuration.Equals("Total", StringComparison.OrdinalIgnoreCase))
        {
            virtualKey.BudgetStartDate = DetermineBudgetStartDate(virtualKey.BudgetDuration);
        }

        virtualKey.UpdatedAt = DateTime.UtcNow;

        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting spend for virtual key with ID {KeyId}.", id);
            return false;
        }
    }

    /// <summary>
    /// Validates a provided virtual key string against stored key hashes.
    /// Checks if the key exists, is enabled, and has not expired.
    /// </summary>
    /// <param name="key">The virtual key string to validate.</param>
    /// <param name="requestedModel">Optional model being requested, to check against allowed models.</param>
    /// <returns>The valid VirtualKey entity if found and valid, otherwise null.</returns>
    public async Task<ConduitLLM.Configuration.Entities.VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogWarning("Empty key provided for validation");
            return null;
        }

        if (!key.StartsWith(VirtualKeyConstants.KeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid key format: doesn't start with required prefix");
            return null;
        }

        // Hash the key for comparison
        string keyHash = HashKey(key);

        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        var virtualKey = await context.VirtualKeys
            .AsNoTracking() // To ensure we're not tracking this entity (read-only)
            .FirstOrDefaultAsync(vk => vk.KeyHash == keyHash);

        if (virtualKey == null)
        {
            _logger.LogWarning("No matching virtual key found");
            return null;
        }

        // Check if key is enabled
        if (!virtualKey.IsEnabled)
        {
            _logger.LogWarning("Virtual key is disabled: {KeyName} (ID: {KeyId})", virtualKey.KeyName, virtualKey.Id);
            return null;
        }

        // Check expiration
        if (virtualKey.ExpiresAt.HasValue && virtualKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("Virtual key has expired: {KeyName} (ID: {KeyId}), expired at {ExpiryDate}", 
                virtualKey.KeyName, virtualKey.Id, virtualKey.ExpiresAt);
            return null;
        }

        // Check budget
        if (virtualKey.MaxBudget.HasValue && virtualKey.CurrentSpend >= virtualKey.MaxBudget.Value)
        {
            _logger.LogWarning("Virtual key budget depleted: {KeyName} (ID: {KeyId}), spent {CurrentSpend}, budget {MaxBudget}", 
                virtualKey.KeyName, virtualKey.Id, virtualKey.CurrentSpend, virtualKey.MaxBudget);
            return null;
        }

        // Check if model is allowed, if model restrictions are in place
        if (!string.IsNullOrEmpty(requestedModel) && !string.IsNullOrEmpty(virtualKey.AllowedModels))
        {
            bool isModelAllowed = IsModelAllowed(requestedModel, virtualKey.AllowedModels);
            
            if (!isModelAllowed)
            {
                _logger.LogWarning("Virtual key {KeyName} (ID: {KeyId}) attempted to access restricted model: {RequestedModel}", 
                    virtualKey.KeyName, virtualKey.Id, requestedModel);
                return null;
            }
        }

        // All validations passed
        _logger.LogInformation("Validated virtual key successfully: {KeyName} (ID: {KeyId})", 
            virtualKey.KeyName, virtualKey.Id);
        return virtualKey;
    }

    /// <summary>
    /// Checks if a requested model is allowed based on the AllowedModels string
    /// </summary>
    private bool IsModelAllowed(string requestedModel, string allowedModels)
    {
        if (string.IsNullOrEmpty(allowedModels))
            return true; // No restrictions
        
        var allowedModelsList = allowedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        // First check for exact match
        if (allowedModelsList.Any(m => string.Equals(m, requestedModel, StringComparison.OrdinalIgnoreCase)))
            return true;
        
        // Then check for wildcard/prefix matches
        foreach (var allowedModel in allowedModelsList)
        {
            // Handle wildcards like "gpt-4*" to match any GPT-4 model
            if (allowedModel.EndsWith("*", StringComparison.OrdinalIgnoreCase) && 
                allowedModel.Length > 1)
            {
                string prefix = allowedModel.Substring(0, allowedModel.Length - 1);
                if (requestedModel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Updates the spend for a specific virtual key.
    /// This should be called after a successful request proxied using the key.
    /// </summary>
    /// <param name="keyId">The ID of the virtual key.</param>
    /// <param name="cost">The cost incurred by the request.</param>
    /// <returns>True if the update was successful, false otherwise (e.g., key not found).</returns>
    public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
    {
        if (cost <= 0) return true; // No cost to add, consider it successful

        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(); 
        ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = await context.VirtualKeys.FindAsync(keyId);

        if (virtualKey == null)
        {
            return false; // Key not found
        }

        try
        {
            // Update spend and timestamp
            virtualKey.CurrentSpend += cost;
            virtualKey.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            
            _logger.LogInformation("Updated spend for key ID {KeyId}. New spend: {CurrentSpend}", keyId, virtualKey.CurrentSpend);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {            
            _logger.LogError(ex, "Concurrency error updating spend for key ID {KeyId}. Attempting retry...", keyId);
            
            // Implement retry strategy
            try
            {
                // Reload the entity with the latest data
                await ex.Entries.Single().ReloadAsync();
                
                // Get a new reference to the entity after reload
                virtualKey = await context.VirtualKeys.FindAsync(keyId);
                if (virtualKey == null)
                {
                    _logger.LogError("After concurrency error, key ID {KeyId} could not be found.", keyId);
                    return false;
                }
                
                // Reapply the spend update to the latest version
                virtualKey.CurrentSpend += cost;
                virtualKey.UpdatedAt = DateTime.UtcNow;
                
                // Try to save again
                await context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated spend after retry for key ID {KeyId}. New spend: {CurrentSpend}", 
                    keyId, virtualKey.CurrentSpend);
                return true;
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Failed to update spend after concurrency retry for key ID {KeyId}.", keyId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spend for key ID {KeyId}.", keyId);
            return false;
        }
    }

    /// <summary>
    /// Checks if the budget period for a key has expired based on its duration and start date,
    /// and resets the spend and start date if necessary.
    /// </summary>
    /// <param name="keyId">The ID of the virtual key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the budget was reset, false otherwise.</returns>
    public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(cancellationToken); 
        ConduitLLM.Configuration.Entities.VirtualKey? virtualKey = await context.VirtualKeys.FindAsync(
            new object[] { keyId }, 
            cancellationToken);

        if (virtualKey == null || 
            string.IsNullOrEmpty(virtualKey.BudgetDuration) || 
            !virtualKey.BudgetStartDate.HasValue)
        {
            // Can't reset budget if key doesn't exist or has no budget duration/start date
            return false;
        }

        DateTime now = DateTime.UtcNow;
        bool needsReset = false;

        // Calculate when the current budget period should end
        if (virtualKey.BudgetDuration.Equals(Configuration.Constants.VirtualKeyConstants.BudgetPeriods.Monthly, 
                                          StringComparison.OrdinalIgnoreCase))
        {
            // For monthly, check if we're in a new month from the start date
            DateTime startDate = virtualKey.BudgetStartDate.Value;
            DateTime periodEnd = new DateTime(
                startDate.Year + (startDate.Month == 12 ? 1 : 0),
                startDate.Month == 12 ? 1 : startDate.Month + 1,
                1,
                0, 0, 0,
                DateTimeKind.Utc).AddDays(-1); // Last day of the month
            
            needsReset = now > periodEnd;
        }
        else if (virtualKey.BudgetDuration.Equals(Configuration.Constants.VirtualKeyConstants.BudgetPeriods.Daily, 
                                              StringComparison.OrdinalIgnoreCase))
        {
            // For daily, check if we're on a different calendar day (UTC)
            needsReset = now.Date > virtualKey.BudgetStartDate.Value.Date;
        }
        // Note: "Weekly" period is not defined in VirtualKeyConstants.BudgetPeriods
        // If we need to support Weekly in the future, uncomment this code and add it to the constants
        /*
        else if (virtualKey.BudgetDuration.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
        {
            // For weekly, check if it's been 7 days or more since the start date
            DateTime startDate = virtualKey.BudgetStartDate.Value;
            DateTime periodEnd = startDate.AddDays(7).Date;
            
            needsReset = now.Date >= periodEnd;
        }
        */

        if (needsReset)
        {
            try
            {
                _logger.LogInformation(
                    "Resetting budget for key ID {KeyId}. Previous spend: {PreviousSpend}, Previous start date: {PreviousStartDate}", 
                    keyId, virtualKey.CurrentSpend, virtualKey.BudgetStartDate);
                
                // Reset the spend
                virtualKey.CurrentSpend = 0;
                
                // Set new budget start date
                virtualKey.BudgetStartDate = DetermineBudgetStartDate(virtualKey.BudgetDuration);
                virtualKey.UpdatedAt = now;
                
                await context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation(
                    "Budget reset completed for key ID {KeyId}. New start date: {NewStartDate}", 
                    keyId, virtualKey.BudgetStartDate);
                
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error resetting budget for key ID {KeyId}", keyId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting budget for key ID {KeyId}", keyId);
                return false;
            }
        }

        return false; // No reset needed
    }

    /// <summary>
    /// Gets detailed info about a virtual key for validation and budget checking.
    /// </summary>
    /// <param name="keyId">The ID of the virtual key to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The virtual key entity if found, otherwise null.</returns>
    public async Task<ConduitLLM.Configuration.Entities.VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
    {
        // Use the correct context factory
        using var context = await _configContextFactory.CreateDbContextAsync(cancellationToken); 
        return await context.VirtualKeys.FindAsync(new object[] { keyId }, cancellationToken);
    }

    // --- Helper Methods ---

    private string GenerateNewKeyString()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(KeyLengthBytes);
        string base64Key = Convert.ToBase64String(randomBytes)
                                .Replace('+', '-')
                                .Replace('/', '_')
                                .TrimEnd('=');
        return VirtualKeyConstants.KeyPrefix + base64Key; // Use constant KeyPrefix
    }

    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] hashBytes = sha256.ComputeHash(keyBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private DateTime? DetermineBudgetStartDate(string? budgetDuration)
    {
        if (string.IsNullOrEmpty(budgetDuration) || 
            budgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Total, StringComparison.OrdinalIgnoreCase))
            return null;
        if (budgetDuration.Equals(VirtualKeyConstants.BudgetPeriods.Monthly, StringComparison.OrdinalIgnoreCase))
            return new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return DateTime.UtcNow.Date; // Default to start of current day (UTC)
    }

    private string GetKeyPrefix(string key)
    {
        // Only needed during key generation response
        int prefixLength = VirtualKeyConstants.KeyPrefix.Length;
        int displayLength = prefixLength + 4; // Show prefix + a few chars
        if (key.Length > displayLength)
        {
            return key[..displayLength] + "...";
        }
        return key;
    }

    /// <summary>
    /// Maps a VirtualKey entity to a VirtualKeyDto.
    /// Explicitly excludes KeyHash and generates KeyPrefix if needed (though usually done at generation).
    /// </summary>
    private VirtualKeyDto MapToDto(ConduitLLM.Configuration.Entities.VirtualKey entity)
    {
        return new VirtualKeyDto
        {
            Id = entity.Id,
            KeyName = entity.KeyName,
            AllowedModels = entity.AllowedModels,
            MaxBudget = entity.MaxBudget,
            CurrentSpend = entity.CurrentSpend,
            BudgetDuration = entity.BudgetDuration,
            BudgetStartDate = entity.BudgetStartDate,
            IsEnabled = entity.IsEnabled,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Metadata = entity.Metadata,
            RateLimitRpm = entity.RateLimitRpm,
            RateLimitRpd = entity.RateLimitRpd
        };
    }
}
