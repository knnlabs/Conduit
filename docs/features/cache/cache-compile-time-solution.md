# Compile-Time Cache Registration Solution

## Recommended Approach: Configuration-Based Registration

Since we control the Docker containers and build process, runtime reflection is unnecessary overhead. Here's the recommended solution:

### 1. Remove CacheDiscoveryHostedService

The service provides no value since:
- No production code uses cache attributes
- All standard regions are pre-registered
- Runtime reflection is slow and unnecessary

### 2. Use Configuration for Custom Regions

Add to `appsettings.json`:

```json
{
  "Cache": {
    "CustomRegions": {
      "my-feature-cache": {
        "defaultTTL": "PT15M",  // ISO 8601 duration
        "maxTTL": "PT1H",
        "useDistributedCache": true,
        "useMemoryCache": true,
        "priority": 50,
        "evictionPolicy": "LRU"
      }
    }
  }
}
```

### 3. Simple Registration Code

```csharp
public static class CacheManagerExtensions
{
    public static IServiceCollection AddCacheInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // ... existing code ...
        
        // Register custom regions from configuration
        var customRegions = configuration.GetSection("Cache:CustomRegions");
        if (customRegions.Exists())
        {
            services.AddSingleton<IHostedService>(sp =>
            {
                var registry = sp.GetRequiredService<ICacheRegistry>();
                var logger = sp.GetRequiredService<ILogger<CacheRegistry>>();
                
                foreach (var region in customRegions.GetChildren())
                {
                    var config = region.Get<CacheRegionConfig>();
                    if (config != null)
                    {
                        registry.RegisterCustomRegion(region.Key, config);
                        logger.LogInformation("Registered custom cache region: {Region}", region.Key);
                    }
                }
                
                return new NoOpHostedService();
            });
        }
        
        return services;
    }
}
```

### 4. Benefits

- **No reflection** - Zero runtime overhead
- **Configuration-driven** - Changes without recompilation
- **Environment-specific** - Different configs for dev/staging/prod
- **Validated at startup** - Fail fast on misconfiguration
- **Simple to understand** - No magic, just configuration

### 5. Migration Path

1. Delete `CacheDiscoveryHostedService`
2. Remove assembly scanning code from `CacheRegistry`
3. Add configuration section for custom regions
4. Update documentation for adding new cache regions

### 6. Future Custom Regions

When a team needs a custom cache region:

1. Add to configuration:
```json
"my-team-cache": {
  "defaultTTL": "PT30M",
  "useDistributedCache": true
}
```

2. Use in code:
```csharp
await _cacheManager.SetAsync("key", value, options => 
{
    options.Region = "my-team-cache";
});
```

No attributes, no reflection, no compilation needed!

## Alternative: Type-Safe Registration

If you prefer compile-time safety over configuration:

```csharp
public static class CustomCacheRegions
{
    public const string MyFeatureCache = "my-feature-cache";
    
    public static void Register(ICacheRegistry registry)
    {
        registry.RegisterCustomRegion(MyFeatureCache, new CacheRegionConfig
        {
            DefaultTTL = TimeSpan.FromMinutes(15),
            UseDistributedCache = true,
            Priority = 50
        });
    }
}

// In startup
services.AddSingleton<IHostedService>(sp =>
{
    var registry = sp.GetRequiredService<ICacheRegistry>();
    CustomCacheRegions.Register(registry);
    return new NoOpHostedService();
});
```

This gives IntelliSense support and compile-time checking while still avoiding reflection.