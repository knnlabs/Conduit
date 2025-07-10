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
} from '@mantine/core';
import {
  IconAlertCircle,
  IconArrowLeft,
} from '@tabler/icons-react';
import { useRouter } from 'next/navigation';

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
  message = 'This feature is currently under development and will be available soon.',
  showTimeline = true,
  onBack,
}: FeatureUnavailableProps) {
  const router = useRouter();

  const handleBack = () => {
    if (onBack) {
      onBack();
    } else {
      router.back();
    }
  };

  return (
    <Center h="100vh" p="md">
      <Card withBorder shadow="sm" radius="md" maw={600} w="100%">
        <Stack gap="lg" align="center" ta="center">
          <ThemeIcon size={60} radius="xl" variant="light" color="blue">
            <IconAlertCircle size={30} />
          </ThemeIcon>

          <div>
            <Title order={2} mb="xs">
              {title}
            </Title>
            <Text c="dimmed">
              {message}
            </Text>
          </div>

          <Badge size="lg" variant="light">
            Feature: {feature}
          </Badge>

          <Button
            leftSection={<IconArrowLeft size={16} />}
            variant="subtle"
            onClick={handleBack}
          >
            Go Back
          </Button>
        </Stack>
      </Card>
    </Center>
  );
}