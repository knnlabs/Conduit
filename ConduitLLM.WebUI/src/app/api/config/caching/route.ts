import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { BackendCacheConfigurationDto, BackendCacheRegionDto, BackendCachePolicyDto } from '@/types/backend-cache-types';
import type { CacheConfig, CacheStats, CacheDataResponse } from '@/types/cache-types';

// GET /api/config/caching - Get cache configuration and statistics
export async function GET() {
  try {
    // Make direct HTTP request to backend since SDK doesn't have the correct method
    const baseUrl = process.env.CONDUIT_ADMIN_API_BASE_URL ?? 'http://localhost:5002';
    const masterKey = process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY ?? '';
    
    const headers = new Headers();
    headers.set('x-api-key', masterKey);
    headers.set('content-type', 'application/json');
    
    const response = await fetch(`${baseUrl}/api/config/caching`, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch cache configuration: ${response.statusText}`);
    }

    const data = await response.json() as BackendCacheConfigurationDto;

    // Transform backend response to match frontend expectations
    const configs: CacheConfig[] = (data.cacheRegions ?? []).map((region: BackendCacheRegionDto) => {
      // Find the matching policy for this region
      const matchingPolicy = (data.cachePolicies ?? []).find((p: BackendCachePolicyDto) => 
        p.id.startsWith(region.id)
      );
      
      return {
        id: region.id,
        name: region.name,
        type: region.type as 'redis' | 'memory' | 'distributed',
        enabled: true,
        ttl: matchingPolicy?.ttl ?? 3600,
        maxSize: matchingPolicy?.maxSize ?? 1024,
        evictionPolicy: (matchingPolicy?.strategy ?? 'lru').toLowerCase() as 'lru' | 'lfu' | 'ttl' | 'random',
        compression: data.configuration?.compressionEnabled ?? true,
        persistent: region.type === 'distributed' || region.type === 'redis',
      };
    });

    // Transform statistics
    const stats: Record<string, CacheStats> = {};
    (data.cacheRegions ?? []).forEach((region: BackendCacheRegionDto) => {
      const sizeStr = region.metrics?.size ?? '0';
      const sizeNumber = parseInt(sizeStr.replace(/[^\d]/g, ''), 10) || 0;
      
      stats[region.id] = {
        hits: 0,
        misses: 0,
        hitRate: region.metrics?.hitRate ?? 0,
        evictions: 0,
        size: sizeNumber,
        entries: region.metrics?.items ?? 0,
        avgLatency: 0.5,
      };
    });
    
    const result: CacheDataResponse = {
      configs,
      stats,
    };

    return NextResponse.json(result);
  } catch (error) {
    return handleSDKError(error);
  }
}

// PUT /api/config/caching - Update cache configuration
export async function PUT(req: NextRequest) {

  try {
    const adminClient = getServerAdminClient();
    const body: unknown = await req.json();
    
    if (typeof body !== 'object' || body === null || !('updates' in body)) {
      return NextResponse.json({ error: 'Invalid request body' }, { status: 400 });
    }
    
    const { updates } = body as { updates: {
      ttl?: number;
      maxSize?: number;
      evictionPolicy?: string;
      enabled?: boolean;
    }};

    // Update cache configuration using the extended API
    const configuration = adminClient.configuration;
    if (!configuration || typeof configuration.updateCacheConfig !== 'function') {
      throw new Error('Configuration service not available');
    }
    
    const result = await configuration.updateCacheConfig({
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

