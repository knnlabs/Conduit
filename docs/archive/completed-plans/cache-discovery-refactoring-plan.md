# Cache Discovery Service Refactoring Plan

## Executive Summary
The `CacheDiscoveryHostedService` currently causes startup hangs due to expensive assembly reflection. This document outlines a plan to refactor the service for better performance while maintaining functionality.

## Current State Analysis

### Problems
1. **Performance**: `Assembly.GetTypes()` is slow, especially in Docker environments
2. **Blocking**: Even as BackgroundService, it impacts startup time
3. **Unused**: No production code uses cache discovery attributes
4. **Scope**: Scans all types in all ConduitLLM assemblies unnecessarily

### Current Usage
- 0 production usages of `[CacheRegion]` attribute
- 0 production usages of `[CustomCacheRegion]` attribute
- 0 production usages of `[CacheConfigurationProvider]` attribute
- Only test code uses these attributes

## Recommended Solution: Hybrid Approach

### Phase 1: Immediate Optimization (1-2 days)
1. **Keep discovery disabled by default** (current state)
2. **Add explicit registration API** for teams that need custom regions:
   ```csharp
   services.AddCacheInfrastructure(config => 
   {
       config.RegisterCustomRegion("my-region", options => { ... });
   });
   ```

### Phase 2: Lazy Discovery (3-5 days)
1. **Implement lazy discovery** that only scans when a region is accessed
2. **Cache discovery results** in Redis for subsequent startups
3. **Add assembly-level hints** to skip assemblies without cache attributes:
   ```csharp
   [assembly: ContainsCacheRegions] // Only scan assemblies with this
   ```

### Phase 3: Source Generator (1-2 weeks, optional)
1. **Create Roslyn source generator** to discover attributes at compile time
2. **Generate static registry** with zero runtime cost
3. **Maintain backward compatibility** with runtime discovery

## Implementation Details

### Optimized Discovery Algorithm
```csharp
public class OptimizedCacheDiscoveryService : BackgroundService
{
    private readonly IMemoryCache _discoveryCache;
    private readonly IDistributedCache _distributedCache;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Check distributed cache for previous results
        var cachedResults = await LoadCachedDiscoveryResults();
        if (cachedResults != null)
        {
            ApplyCachedResults(cachedResults);
            return;
        }
        
        // 2. Perform optimized discovery
        var assemblies = GetTargetAssemblies();
        var results = new ConcurrentBag<DiscoveryResult>();
        
        await Parallel.ForEachAsync(assemblies, async (assembly, ct) =>
        {
            // Skip if no cache attributes hint
            if (!HasCacheAttributesHint(assembly))
                return;
                
            var types = assembly.GetExportedTypes(); // Public types only
            await ScanTypesAsync(types, results, ct);
        });
        
        // 3. Cache results for next startup
        await CacheDiscoveryResults(results);
    }
    
    private bool HasCacheAttributesHint(Assembly assembly)
    {
        // Quick check for assembly-level attribute
        return assembly.GetCustomAttribute<ContainsCacheRegionsAttribute>() != null;
    }
}
```

### Lazy Discovery Implementation
```csharp
public class LazyDiscoveryCacheRegistry : ICacheRegistry
{
    private readonly Lazy<Task> _discoveryTask;
    private readonly SemaphoreSlim _discoverySemaphore = new(1);
    
    public async Task<CacheRegionConfig?> GetRegionConfigAsync(CacheRegion region)
    {
        // Check if already registered
        if (_regions.ContainsKey(region))
            return _regions[region];
            
        // Trigger discovery for calling assembly only
        var callingAssembly = Assembly.GetCallingAssembly();
        await DiscoverAssemblyIfNeededAsync(callingAssembly);
        
        return _regions.TryGetValue(region, out var config) ? config : null;
    }
}
```

## Migration Strategy

1. **Keep current behavior** (discovery disabled) as default
2. **Add new APIs** without breaking existing code
3. **Provide migration guide** for teams using custom regions
4. **Monitor performance** metrics after deployment

## Success Metrics

- Startup time: < 5 seconds (currently hangs indefinitely)
- Discovery time (when enabled): < 100ms per assembly
- Memory usage: < 10MB for discovery metadata
- Zero impact when discovery is disabled

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing functionality | Keep discovery disabled by default |
| Performance regression | Implement comprehensive benchmarks |
| Complex migration | Provide clear documentation and examples |

## Timeline

- Week 1: Implement Phase 1 (explicit registration)
- Week 2-3: Implement Phase 2 (lazy discovery)
- Week 4+: Consider Phase 3 (source generator) based on usage

## Conclusion

This refactoring plan addresses the performance issues while maintaining backward compatibility and preparing for future extensibility. The phased approach allows for immediate relief while building toward a more robust solution.