import { HomePageClient } from '@/components/pages/HomePageClient';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { getServerCoreClient } from '@/lib/server/coreClient';
import { processHealthStatus, createErrorHealthStatus } from '@/lib/utils/health-status';

// Force dynamic rendering to ensure health check runs at request time
export const dynamic = 'force-dynamic';

// Server Component - fetches data server-side
export default async function HomePage() {
  // Fetch health status server-side
  const healthData = await getHealthStatus();
  
  return <HomePageClient initialHealthData={healthData} />;
}

async function getHealthStatus() {
  try {
    // During build time, we don't have access to the admin client
    // Return default values to allow static generation
    if (!process.env.CONDUIT_API_TO_API_BACKEND_AUTH_KEY) {
      const errorStatus = createErrorHealthStatus('No backend auth key configured');
      return {
        ...errorStatus,
        lastChecked: errorStatus.lastChecked.toISOString(),
      };
    }

    const adminClient = getServerAdminClient();
    const health = await adminClient.system.getHealth();
    
    // Try to get SignalR status from Core API using SDK
    let signalrStatus: 'healthy' | 'degraded' | 'unavailable' = 'unavailable';
    try {
      const coreClient = await getServerCoreClient();
      const coreHealth = await coreClient.health.checkReady();
      
      // Find SignalR health check in the Core API response
      const signalrCheck = coreHealth.checks?.find(
        check => check.name?.toLowerCase() === 'signalr'
      );
      
      if (signalrCheck) {
        // Map Core API health status string values
        switch (signalrCheck.status.toLowerCase()) {
          case 'healthy':
            signalrStatus = 'healthy';
            break;
          case 'degraded':
            signalrStatus = 'degraded';
            break;
          default:
            signalrStatus = 'unavailable';
        }
      }
    } catch (error) {
      console.warn('Failed to fetch Core API health for SignalR status:', error);
      // Keep signalrStatus as 'unavailable'
    }
    
    // Use the shared health processing function
    const processed = processHealthStatus(health, signalrStatus);
    
    return {
      ...processed,
      lastChecked: processed.lastChecked.toISOString(),
    };
  } catch (error) {
    console.error('Failed to fetch health status:', error);
    const errorStatus = createErrorHealthStatus('Failed to fetch health status');
    return {
      ...errorStatus,
      lastChecked: errorStatus.lastChecked.toISOString(),
    };
  }
}