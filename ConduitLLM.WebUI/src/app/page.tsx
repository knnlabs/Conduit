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
import { getServerCoreClient } from '@/lib/server/coreClient';
import { mapHealthStatus, isNoProvidersIssue, HealthComponents } from '@/lib/constants/health';

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
    const providerCheck = health.checks?.[HealthComponents.PROVIDERS];
    const hasNoProviders = isNoProvidersIssue(providerCheck?.description);
    
    // Use the provider check status as the Core API status
    // Since providers ARE the core functionality
    const coreApiStatus = providerCheck ? mapHealthStatus(providerCheck.status) : 'unavailable';
    
    return {
      adminApi: mapHealthStatus(health.status),
      coreApi: coreApiStatus,
      isNoProvidersIssue: hasNoProviders,
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