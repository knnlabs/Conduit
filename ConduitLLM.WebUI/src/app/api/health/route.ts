import { NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET() {
  try {
    const adminClient = await getServerAdminClient();
    console.log('[Health API] Fetching health status from Admin API');
    const health = await adminClient.system.getHealth();
    console.log('[Health API] Health response:', JSON.stringify(health, null, 2));
    
    // Extract provider status from health checks
    const providerCheck = health.checks?.providers;
    const isNoProvidersIssue = providerCheck?.status === 'degraded' && 
      (providerCheck?.description?.toLowerCase().includes('no enabled providers') || 
       providerCheck?.description?.toLowerCase().includes('no providers'));
    
    // Admin API is available if we got a response, regardless of overall health status
    const adminApiStatus = health.checks ? 
      (health.checks.database?.status === 'healthy' ? 'healthy' : 'degraded') : 
      'unavailable';
    
    return NextResponse.json({
      adminApi: adminApiStatus,
      coreApi: providerCheck?.status === 'healthy' ? 'healthy' :
               providerCheck?.status === 'degraded' ? 'degraded' : 
               providerCheck?.status === 'unhealthy' ? 'unhealthy' : 'unavailable',
      isNoProvidersIssue: isNoProvidersIssue || false,
      coreApiMessage: providerCheck?.description,
      lastChecked: new Date().toISOString(),
    });
  } catch (error) {
    console.error('Health check failed:', error);
    return NextResponse.json({
      adminApi: 'unavailable',
      coreApi: 'unavailable',
      isNoProvidersIssue: false,
      lastChecked: new Date().toISOString(),
    }, { status: 503 });
  }
}