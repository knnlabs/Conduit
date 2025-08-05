'use client';

import { 
  Title, 
  Text, 
  Card, 
  Group, 
  Stack, 
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

export function HomePageClient() {
  const router = useRouter();

  const quickAccessCards = [
    {
      title: 'LLM Providers',
      description: 'Manage and configure language model providers',
      icon: IconServer,
      color: 'blue',
      href: '/llm-providers'
    },
    {
      title: 'Virtual Keys',
      description: 'Create and manage virtual API keys',
      icon: IconKey,
      color: 'green',
      href: '/virtual-keys'
    },
    {
      title: 'System Performance',
      description: 'Monitor system performance metrics',
      icon: IconChartBar,
      color: 'orange',
      href: '/system-performance'
    },
    {
      title: 'Chat Interface',
      description: 'Test language models with the chat interface',
      icon: IconMessageChatbot,
      color: 'purple',
      href: '/chat'
    },
    {
      title: 'Image Generation',
      description: 'Generate images using AI models',
      icon: IconPhoto,
      color: 'pink',
      href: '/images'
    },
    {
      title: 'Video Generation',
      description: 'Create videos with AI-powered tools',
      icon: IconVideo,
      color: 'red',
      href: '/videos'
    },
    {
      title: 'Audio Processing',
      description: 'Process and generate audio content',
      icon: IconMicrophone,
      color: 'teal',
      href: '/audio'
    },
    {
      title: 'Settings',
      description: 'Configure system settings and preferences',
      icon: IconSettings,
      color: 'gray',
      href: '/settings'
    }
  ];

  return (
    <Stack gap="xl">
      <div>
        <Title order={1} mb="sm">
          Welcome to Conduit WebUI
        </Title>
        <Text size="lg" c="dimmed">
          Your centralized platform for managing AI providers and services
        </Text>
      </div>

      <Grid>
        {quickAccessCards.map((card) => (
          <Grid.Col key={card.title} span={{ base: 12, sm: 6, md: 4, lg: 3 }}>
            <Card 
              padding="lg" 
              h="100%" 
              style={{ cursor: 'pointer' }}
              onClick={() => router.push(card.href)}
            >
              <Stack h="100%" gap="md">
                <Group>
                  <ThemeIcon size="lg" variant="light" color={card.color}>
                    <card.icon size={24} />
                  </ThemeIcon>
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
    </Stack>
  );
}