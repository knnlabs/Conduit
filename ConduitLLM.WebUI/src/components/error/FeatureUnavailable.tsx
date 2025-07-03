'use client';

import React from 'react';
import {
  Alert,
  Card,
  Center,
  Stack,
  Text,
  Title,
  ThemeIcon,
  Button,
  Group,
  Badge,
  Timeline,
} from '@mantine/core';
import {
  IconAlertCircle,
  IconRocket,
  IconCode,
  IconCheck,
  IconClock,
  IconArrowLeft,
} from '@tabler/icons-react';
import { useRouter } from 'next/navigation';
import { getFeatureMessage } from '@/lib/placeholders/backend-placeholders';

interface FeatureUnavailableProps {
  feature: string;
  title?: string;
  message?: string;
  showTimeline?: boolean;
  onBack?: () => void;
}

/**
 * Component to display when a feature is not yet available
 */
export function FeatureUnavailable({
  feature,
  title = 'Feature Coming Soon',
  message,
  showTimeline = true,
  onBack,
}: FeatureUnavailableProps) {
  const router = useRouter();
  const featureMessage = message || getFeatureMessage(feature);

  const handleBack = () => {
    if (onBack) {
      onBack();
    } else {
      router.back();
    }
  };

  return (
    <Center mih="50vh">
      <Card shadow="md" p="xl" maw={600} w="100%">
        <Stack align="center" gap="md">
          <ThemeIcon size={80} radius="xl" color="blue" variant="light">
            <IconRocket size={50} />
          </ThemeIcon>

          <Stack align="center" gap="xs">
            <Title order={2}>{title}</Title>
            <Badge size="lg" variant="light" color="blue">
              In Development
            </Badge>
          </Stack>

          <Alert
            icon={<IconAlertCircle size={16} />}
            title="Feature Status"
            color="blue"
            variant="light"
            w="100%"
          >
            <Text size="sm">{featureMessage}</Text>
          </Alert>

          {showTimeline && (
            <Card w="100%" p="md" bg="gray.0">
              <Text size="sm" fw={600} mb="md">Development Timeline</Text>
              <Timeline active={1} bulletSize={24} lineWidth={2}>
                <Timeline.Item
                  bullet={<IconCheck size={12} />}
                  title="Feature Designed"
                >
                  <Text c="dimmed" size="xs">Requirements and API design completed</Text>
                </Timeline.Item>
                
                <Timeline.Item
                  bullet={<IconCode size={12} />}
                  title="Backend Development"
                  lineVariant="dashed"
                >
                  <Text c="dimmed" size="xs">API endpoints being implemented</Text>
                </Timeline.Item>
                
                <Timeline.Item
                  bullet={<IconClock size={12} />}
                  title="Testing & Release"
                  color="gray"
                >
                  <Text c="dimmed" size="xs">Coming in next release</Text>
                </Timeline.Item>
              </Timeline>
            </Card>
          )}

          <Group>
            <Button
              variant="light"
              leftSection={<IconArrowLeft size={16} />}
              onClick={handleBack}
            >
              Go Back
            </Button>
          </Group>
        </Stack>
      </Card>
    </Center>
  );
}

/**
 * Hook to check if a feature is available
 */
export function useFeatureAvailability(feature: string) {
  const [isAvailable, setIsAvailable] = React.useState(true);
  const [isChecking, setIsChecking] = React.useState(true);

  React.useEffect(() => {
    // In a real implementation, this would check with the backend
    const checkAvailability = async () => {
      try {
        // Simulate API check
        await new Promise(resolve => setTimeout(resolve, 100));
        
        const unavailableFeatures = [
          'security-event-reporting',
          'threat-detection',
          'provider-incidents',
          'audio-usage-detailed',
          'realtime-sessions',
          'analytics-export',
        ];
        
        setIsAvailable(!unavailableFeatures.includes(feature));
      } finally {
        setIsChecking(false);
      }
    };

    checkAvailability();
  }, [feature]);

  return {
    isAvailable,
    isChecking,
  };
}