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
  Loader,
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
import { useFeatureMessage, useIsFeatureAvailable } from '@/hooks/api/useFeatureAvailability';

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
  const { featureInfo, isChecking } = useIsFeatureAvailable(feature);
  const defaultMessage = useFeatureMessage(feature);
  const featureMessage = message || defaultMessage;

  const handleBack = () => {
    if (onBack) {
      onBack();
    } else {
      router.back();
    }
  };

  const getStatusBadge = () => {
    if (isChecking) {
      return (
        <Badge size="lg" variant="light" color="gray">
          <Group gap={4}>
            <Loader size={12} />
            Checking...
          </Group>
        </Badge>
      );
    }
    
    const status = featureInfo?.status || 'in_development';
    const statusConfig = {
      'available': { color: 'green', label: 'Available' },
      'coming_soon': { color: 'blue', label: 'Coming Soon' },
      'in_development': { color: 'orange', label: 'In Development' },
      'not_planned': { color: 'gray', label: 'Not Planned' },
    };
    
    const config = statusConfig[status] || statusConfig['in_development'];
    
    return (
      <Badge size="lg" variant="light" color={config.color}>
        {config.label}
      </Badge>
    );
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
            {getStatusBadge()}
          </Stack>

          <Alert
            icon={<IconAlertCircle size={16} />}
            title="Feature Status"
            color="blue"
            variant="light"
            w="100%"
          >
            <Stack gap="xs">
              <Text size="sm">{featureMessage}</Text>
              {featureInfo?.releaseDate && (
                <Text size="xs" c="dimmed">
                  Expected release: {new Date(featureInfo.releaseDate).toLocaleDateString()}
                </Text>
              )}
              {featureInfo?.version && (
                <Text size="xs" c="dimmed">
                  Target version: {featureInfo.version}
                </Text>
              )}
            </Stack>
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

