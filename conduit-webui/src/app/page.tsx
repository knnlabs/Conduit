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
  IconSettings
} from '@tabler/icons-react';
import { useRouter } from 'next/navigation';
import { useConnectionStore } from '@/stores/useConnectionStore';

export default function HomePage() {
  const router = useRouter();
  const { status } = useConnectionStore();

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
      case 'connected': return 'green';
      case 'connecting': return 'yellow';
      case 'error': return 'red';
      default: return 'gray';
    }
  };

  const getStatusText = (status: string) => {
    switch (status) {
      case 'connected': return 'Connected';
      case 'connecting': return 'Connecting...';
      case 'reconnecting': return 'Reconnecting...';
      case 'error': return 'Connection Error';
      default: return 'Disconnected';
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
            <Badge color={getStatusColor(status.coreApi)} variant="dot">
              Core API: {getStatusText(status.coreApi)}
            </Badge>
            <Badge color={getStatusColor(status.adminApi)} variant="dot">
              Admin API: {getStatusText(status.adminApi)}
            </Badge>
            <Badge color={getStatusColor(status.signalR)} variant="dot">
              SignalR: {getStatusText(status.signalR)}
            </Badge>
          </Group>
          <Text size="sm" c="dimmed">
            Real-time features {status.signalR === 'connected' ? 'are active' : 'will be available once SignalR connection is established'}.
            {status.lastCheck && ` Last checked: ${status.lastCheck.toLocaleTimeString()}`}
          </Text>
        </Stack>
      </Card>
    </Stack>
  );
}