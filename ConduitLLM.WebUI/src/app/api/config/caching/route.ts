import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
// GET /api/config/caching - Get cache configuration and statistics
export async function GET(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    
    // Fetch real cache configuration
    const cacheConfig = await adminClient.configuration.getCachingConfiguration();

    // Transform to match frontend expectations
    // For now, create static regions based on configuration
    const regions = [
      {
        id: 'provider-responses',
        name: 'Provider Responses',
        type: 'memory' as 'redis' | 'memory' | 'distributed',
        enabled: true,
        ttl: cacheConfig.defaultTTLSeconds || 3600,
        maxSize: Math.floor((cacheConfig.maxMemorySizeMB || 1024) * 0.4),
        evictionPolicy: (cacheConfig.evictionPolicy || 'lru') as 'lru' | 'lfu' | 'ttl' | 'random',
        compression: cacheConfig.compressionEnabled ?? true,
        persistent: false,
      },
      {
        id: 'embeddings',
        name: 'Embeddings Cache',
        type: (cacheConfig.distributedCacheEnabled ? 'redis' : 'memory') as 'redis' | 'memory' | 'distributed',
        enabled: cacheConfig.distributedCacheEnabled ?? false,
        ttl: (cacheConfig.defaultTTLSeconds || 3600) * 24,
        maxSize: Math.floor((cacheConfig.maxMemorySizeMB || 1024) * 0.3),
        evictionPolicy: (cacheConfig.evictionPolicy || 'lru') as 'lru' | 'lfu' | 'ttl' | 'random',
        compression: cacheConfig.compressionEnabled ?? true,
        persistent: cacheConfig.distributedCacheEnabled ?? false,
      },
      {
        id: 'model-metadata',
        name: 'Model Metadata',
        type: 'memory' as 'redis' | 'memory' | 'distributed',
        enabled: true,
        ttl: 600,
        maxSize: Math.floor((cacheConfig.maxMemorySizeMB || 1024) * 0.1),
        evictionPolicy: 'ttl' as 'lru' | 'lfu' | 'ttl' | 'random',
        compression: false,
        persistent: false,
      },
      {
        id: 'rate-limits',
        name: 'Rate Limit Counters',
        type: 'memory' as 'redis' | 'memory' | 'distributed',
        enabled: true,
        ttl: 60,
        maxSize: Math.floor((cacheConfig.maxMemorySizeMB || 1024) * 0.1),
        evictionPolicy: 'ttl' as 'lru' | 'lfu' | 'ttl' | 'random',
        compression: false,
        persistent: false,
      },
      {
        id: 'auth-tokens',
        name: 'Auth Token Cache',
        type: (cacheConfig.distributedCacheEnabled ? 'distributed' : 'memory') as 'redis' | 'memory' | 'distributed',
        enabled: true,
        ttl: 1800,
        maxSize: Math.floor((cacheConfig.maxMemorySizeMB || 1024) * 0.1),
        evictionPolicy: 'ttl' as 'lru' | 'lfu' | 'ttl' | 'random',
        compression: false,
        persistent: cacheConfig.distributedCacheEnabled ?? false,
      },
    ];
    
    const response = {
      configs: regions,
      stats: {},
    };

    return NextResponse.json(response);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/config/caching - Update cache configuration
export async function PUT(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const { cacheId, updates } = await req.json();

    // Update cache configuration using the extended API
    const result = await adminClient.configuration.updateCacheConfig({
      defaultTtlSeconds: updates.ttl,
      maxSizeBytes: updates.maxSize ? updates.maxSize * 1024 * 1024 : undefined, // Convert MB to bytes
      strategy: updates.evictionPolicy as 'lru' | 'lfu' | 'ttl' | 'adaptive' | undefined,
      enabled: updates.enabled,
    });
    
    return NextResponse.json({ success: true, config: result });
  } catch (error) {
    return handleSDKError(error);
  }
}

