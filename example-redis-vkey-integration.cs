// Example: How to integrate Redis Virtual Key caching in Program.cs

// In ConduitLLM.Http/Program.cs, add after existing Redis setup:

// Register Redis Virtual Key Cache (if Redis is available)
if (!string.IsNullOrEmpty(redisConnectionString))
{
    // Use cached service for high-performance validation
    builder.Services.AddSingleton<IVirtualKeyCache, RedisVirtualKeyCache>();
    builder.Services.AddScoped<IApiVirtualKeyService, CachedApiVirtualKeyService>();
    
    Console.WriteLine("[Conduit] Using Redis-cached Virtual Key validation (high-performance mode)");
}
else
{
    // Fallback to direct database service
    builder.Services.AddScoped<IApiVirtualKeyService, ApiVirtualKeyService>();
    
    Console.WriteLine("[Conduit] Using direct database Virtual Key validation (fallback mode)");
}

// Critical: Integrate invalidation with admin operations
// In ConduitLLM.Admin/Services/AdminVirtualKeyService.cs:

public class AdminVirtualKeyService : IAdminVirtualKeyService
{
    private readonly IVirtualKeyRepository _repository;
    private readonly IVirtualKeyCache _cache; // Inject cache for invalidation
    
    public async Task<bool> UpdateVirtualKeyAsync(UpdateVirtualKeyDto dto)
    {
        var success = await _repository.UpdateAsync(entity);
        
        if (success)
        {
            // CRITICAL: Invalidate cache immediately
            await _cache.InvalidateVirtualKeyAsync(entity.KeyHash);
        }
        
        return success;
    }
    
    public async Task<bool> DisableVirtualKeyAsync(int id)
    {
        var key = await _repository.GetByIdAsync(id);
        if (key == null) return false;
        
        key.IsEnabled = false;
        var success = await _repository.UpdateAsync(key);
        
        if (success)
        {
            // SECURITY CRITICAL: Immediate invalidation
            await _cache.InvalidateVirtualKeyAsync(key.KeyHash);
        }
        
        return success;
    }
}

// Integration with BatchSpendUpdateService:
// Modify the batch service to invalidate cached keys when spend is updated

public class BatchSpendUpdateService : BackgroundService
{
    private readonly IVirtualKeyCache _cache;
    
    public async Task<int> FlushPendingUpdatesAsync()
    {
        // ... existing spend update logic ...
        
        // After successful database update:
        if (affectedRows > 0)
        {
            // Invalidate all updated keys
            var keyHashes = updates.Select(u => GetKeyHashById(u.Key)).ToArray();
            await _cache.InvalidateVirtualKeysAsync(keyHashes);
        }
        
        return updates.Length;
    }
}