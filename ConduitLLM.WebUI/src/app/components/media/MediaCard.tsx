'use client';

import React from 'react';
import { 
  Card, 
  Stack, 
  Text, 
  Badge, 
  Box,
  Center,
  Progress,
  Alert
} from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';

/**
 * Common props for media cards
 */
export interface MediaCardProps {
  children?: React.ReactNode;
  prompt?: string;
  status?: 'pending' | 'running' | 'completed' | 'failed' | 'error';
  progress?: number;
  error?: string;
  metadata?: React.ReactNode;
  actions?: React.ReactNode;
  onClick?: () => void;
  shadow?: string;
  padding?: string;
  radius?: string;
  withBorder?: boolean;
}

/**
 * Reusable media card component for displaying media items
 */
export function MediaCard({
  children,
  prompt,
  status,
  progress,
  error,
  metadata,
  actions,
  onClick,
  shadow = 'sm',
  padding = 'lg',
  radius = 'md',
  withBorder = true
}: MediaCardProps) {
  // Handle pending/loading state
  if (status === 'pending' || status === 'running') {
    return (
      <Card shadow={shadow} padding={padding} radius={radius} withBorder={withBorder}>
        <Stack gap="sm">
          {prompt && <Text size="sm" lineClamp={2}>{prompt}</Text>}
          <Badge color="blue" variant="light">
            {status === 'pending' ? 'Queued' : 'Generating'}
          </Badge>
          {progress !== undefined && (
            <Progress value={progress} size="sm" animated />
          )}
          {actions}
        </Stack>
      </Card>
    );
  }

  // Handle error state
  if (status === 'failed' || status === 'error' || error) {
    return (
      <Card shadow={shadow} padding={padding} radius={radius} withBorder={withBorder}>
        <Stack gap="sm">
          {prompt && <Text size="sm" lineClamp={2}>{prompt}</Text>}
          <Badge color="red" variant="light">Failed</Badge>
          {error && (
            <Alert 
              icon={<IconAlertCircle size={16} />} 
              color="red" 
              variant="light"
            >
              {error}
            </Alert>
          )}
          {actions}
        </Stack>
      </Card>
    );
  }

  // Normal media display
  return (
    <Card 
      shadow={shadow} 
      padding={padding} 
      radius={radius} 
      withBorder={withBorder}
      onClick={onClick}
      style={{ cursor: onClick ? 'pointer' : 'default' }}
    >
      {children && (
        <Card.Section>
          {children}
        </Card.Section>
      )}
      
      {(prompt ?? metadata ?? actions) && (
        <Stack gap="sm" mt={children ? 'md' : 0}>
          {prompt && (
            <Text size="sm" fw={500} lineClamp={2}>
              {prompt}
            </Text>
          )}
          
          {metadata}
          
          {actions}
        </Stack>
      )}
    </Card>
  );
}

/**
 * Helper component for media content sections
 */
export interface MediaContentProps {
  children: React.ReactNode;
  height?: number | string;
  backgroundColor?: string;
}

export function MediaContent({ 
  children, 
  height = 250, 
  backgroundColor = '#000' 
}: MediaContentProps) {
  return (
    <Box style={{ position: 'relative', height, backgroundColor }}>
      {children}
    </Box>
  );
}

/**
 * Helper component for empty media placeholder
 */
export interface MediaPlaceholderProps {
  message?: string;
  height?: number | string;
}

export function MediaPlaceholder({ 
  message = 'No media available', 
  height = 200 
}: MediaPlaceholderProps) {
  return (
    <Center h={height} bg="gray.1">
      <Text c="dimmed">{message}</Text>
    </Center>
  );
}