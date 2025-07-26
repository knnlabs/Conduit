'use client';

import { 
  Title, 
  Text, 
  Card, 
  Group, 
  Stack, 
  Badge,
  Button,
  Grid,
  ThemeIcon,
  Alert
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
import { useRouter } from 'next/navigation';
import { StatusHoverCard } from '@/components/common/StatusHoverCard';
import type { HealthCheckDetail } from '@/types/health';

interface HealthData {
  adminApi: 'healthy' | 'degraded' | 'unavailable';
  coreApi: 'healthy' | 'degraded' | 'unavailable';
  signalr: 'healthy' | 'degraded' | 'unavailable';
  isNoProvidersIssue: boolean;
  coreApiMessage?: string;
  lastChecked: string;
  adminChecks?: Record<string, HealthCheckDetail>;
  coreChecks?: Record<string, HealthCheckDetail>;
}

interface HomePageClientProps {
  initialHealthData: HealthData;
}

export function HomePageClient({ initialHealthData }: HomePageClientProps) {
  const router = useRouter();
  const healthData = initialHealthData;

  const quickAccessCards = [
    {
      title: 'Chat Interface',
      description: 'Test LLM models with interactive chat',
      icon: IconMessageChatbot,
      color: 'blue',
      href: '/chat',
    },
    {
      title: 'Virtual Keys',
      description: 'Manage API keys and budgets',
      icon: IconKey,
      color: 'green',
      href: '/virtualkeys',
    },
    {
      title: 'Providers',
      description: 'Configure LLM providers',
      icon: IconServer,
      color: 'purple',
      href: '/llm-providers',
    },
    {
      title: 'Analytics',
      description: 'View cost and usage analytics',
      icon: IconChartBar,
      color: 'orange',
      href: '/cost-dashboard',
    },
    {
      title: 'Image Generation',
      description: 'Generate images with AI',
      icon: IconPhoto,
      color: 'pink',
      href: '/image-generation',
    },
    {
      title: 'Video Generation',
      description: 'Create videos with AI',
      icon: IconVideo,
      color: 'red',
      href: '/video-generation',
    },
    {
      title: 'Audio Processing',
      description: 'Transcription and TTS',
      icon: IconMicrophone,
      color: 'teal',
      href: '/audio-test',
    },
    {
      title: 'Configuration',
      description: 'System settings and preferences',
      icon: IconSettings,
      color: 'gray',
      href: '/configuration',
    },
  ];

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'healthy':
      case 'connected': return 'green';
      case 'degraded':
      case 'connecting': return 'yellow';
      case 'unhealthy':
      case 'unavailable':
      case 'error': return 'red';
      default: return 'gray';
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case 'healthy': return 'HEALTHY';
      case 'degraded': return 'DEGRADED';
      case 'unhealthy': return 'UNHEALTHY';
      case 'unavailable': return 'UNAVAILABLE';
      case 'connected': return 'Connected';
      case 'connecting': return 'Connecting...';
      case 'disconnected': return 'Disconnected';
      default: return 'Unknown';
    }
  };

  return (
    <Stack gap="xl">
      <div>
        <Title order={1} size="h1" mb="sm">
          Welcome to Conduit WebUI
        </Title>
        <Text size="lg" c="dimmed">
          Manage your LLM infrastructure, test models, and monitor usage all in one place.
        </Text>
      </div>

      {/* Show prominent alert if no providers are configured */}
      {healthData.isNoProvidersIssue && (
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Action Required: Configure LLM Providers" 
          color="yellow"
          variant="light"
        >
          <Stack gap="sm">
            <Text size="sm">
              The Core API is running in a degraded state because no LLM providers are configured. 
              You need to add at least one provider (OpenAI, Anthropic, etc.) to enable AI functionality.
            </Text>
            <Group>
              <Button 
                variant="filled" 
                color="yellow" 
                size="sm"
                onClick={() => router.push('/llm-providers')}
              >
                Configure Providers
              </Button>
              <Button 
                variant="subtle" 
                color="yellow" 
                size="sm"
                onClick={() => router.push('/getting-started')}
              >
                View Setup Guide
              </Button>
            </Group>
          </Stack>
        </Alert>
      )}

      <Grid>
        {quickAccessCards.map((card) => (
          <Grid.Col span={{ base: 12, sm: 6, md: 4, lg: 3 }} key={card.title}>
            <Card 
              h="100%" 
              p="lg" 
              style={{ cursor: 'pointer' }}
              onClick={() => router.push(card.href)}
            >
              <Stack gap="md">
                <Group>
                  <ThemeIcon size="lg" color={card.color} variant="light">
                    <card.icon size={24} />
                  </ThemeIcon>
                  <Badge color={card.color} variant="light" size="sm">
                    Feature
                  </Badge>
                </Group>
                
                <div>
                  <Title order={4} mb="xs">
                    {card.title}
                  </Title>
                  <Text size="sm" c="dimmed">
                    {card.description}
                  </Text>
                </div>
                
                <Button 
                  variant="light" 
                  color={card.color} 
                  size="xs" 
                  fullWidth
                  mt="auto"
                >
                  Open
                </Button>
              </Stack>
            </Card>
          </Grid.Col>
        ))}
      </Grid>

      <Card p="lg">
        <Stack gap="md">
          <Title order={3}>System Status</Title>
          <Group>
            <StatusHoverCard
              status={healthData.coreApi}
              title="Core API"
              lastChecked={new Date(healthData.lastChecked)}
              checks={healthData.coreChecks}
              message={healthData.coreApiMessage}
            >
              <Badge color={getStatusColor(healthData.coreApi)} variant="dot">
                Core API: {getStatusText(healthData.coreApi)}
              </Badge>
            </StatusHoverCard>
            
            <StatusHoverCard
              status={healthData.adminApi}
              title="Admin API"
              lastChecked={new Date(healthData.lastChecked)}
              checks={healthData.adminChecks}
            >
              <Badge color={getStatusColor(healthData.adminApi)} variant="dot">
                Admin API: {getStatusText(healthData.adminApi)}
              </Badge>
            </StatusHoverCard>
            
            <Badge color={getStatusColor(healthData.signalr)} variant="dot">
              Real-time Updates: {getStatusText(healthData.signalr)}
            </Badge>
          </Group>
          <Text size="sm" c="dimmed">
            Real-time features {healthData.signalr === 'healthy' ? 'are active' : 'will be available once connection is established'}.
            {healthData.lastChecked && ` Last checked: ${new Date(healthData.lastChecked).toLocaleTimeString()}`}
          </Text>
        </Stack>
      </Card>
    </Stack>
  );
}