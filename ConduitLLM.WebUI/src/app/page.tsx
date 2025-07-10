import { 
  Title, 
  Text, 
  Card, 
  Group, 
  Stack, 
  Badge,
  Button,
  Grid,
  ThemeIcon
} from '@mantine/core';
import { 
  IconServer, 
  IconKey, 
  IconChartBar, 
  IconMessageChatbot,
  IconPhoto,
  IconVideo,
  IconMicrophone,
  IconSettings,
  IconAlertCircle
} from '@tabler/icons-react';
import { Alert } from '@mantine/core';
import { HomePageClient } from '@/components/pages/HomePageClient';
import { getServerAdminClient } from '@/lib/server/adminClient';

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
      return {
        adminApi: 'unavailable' as const,
        coreApi: 'unavailable' as const,
        isNoProvidersIssue: false,
        lastChecked: new Date().toISOString(),
      };
    }

    const adminClient = await getServerAdminClient();
    const health = await adminClient.system.getHealth();
    
    // Extract provider status from health checks
    const providerCheck = health.checks?.providers;
    const isNoProvidersIssue = providerCheck?.status === 'degraded' && 
      (providerCheck?.description?.toLowerCase().includes('no enabled providers') || 
       providerCheck?.description?.toLowerCase().includes('no providers'));
    
    return {
      adminApi: health.status === 'healthy' ? 'healthy' as const : 
                health.status === 'degraded' ? 'degraded' as const : 'unavailable' as const,
      coreApi: providerCheck?.status === 'healthy' ? 'healthy' as const :
               providerCheck?.status === 'degraded' ? 'degraded' as const : 'unavailable' as const,
      isNoProvidersIssue: isNoProvidersIssue || false,
      coreApiMessage: providerCheck?.description,
      lastChecked: new Date().toISOString(),
    };
  } catch (error) {
    console.error('Failed to fetch health status:', error);
    return {
      adminApi: 'unavailable' as const,
      coreApi: 'unavailable' as const,
      isNoProvidersIssue: false,
      lastChecked: new Date().toISOString(),
    };
  }
}