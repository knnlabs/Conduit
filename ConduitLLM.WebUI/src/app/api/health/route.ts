import { NextResponse } from 'next/server';
import { getServerAdminClient } from '@/lib/server/adminClient';

export async function GET() {
  try {
    const adminClient = await getServerAdminClient();
    const health = await adminClient.system.getHealth();
    
    // Extract provider status from health checks
    const providerCheck = health.checks?.providers;
    const isNoProvidersIssue = providerCheck?.status === 'degraded' && 
      (providerCheck?.description?.toLowerCase().includes('no enabled providers') || 
       providerCheck?.description?.toLowerCase().includes('no providers'));
    
    return NextResponse.json({
      adminApi: health.status === 'healthy' ? 'healthy' : 
                health.status === 'degraded' ? 'degraded' : 'unavailable',
      coreApi: providerCheck?.status === 'healthy' ? 'healthy' :
               providerCheck?.status === 'degraded' ? 'degraded' : 'unavailable',
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