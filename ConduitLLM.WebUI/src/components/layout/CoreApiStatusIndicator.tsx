'use client';

import { Indicator, ThemeIcon, Tooltip, Text, Stack, Button } from '@mantine/core';
import { IconServer, IconAlertTriangle, IconCheck, IconX } from '@tabler/icons-react';
import { useRouter } from 'next/navigation';
import { BackendHealthStatus } from '@/hooks/useBackendHealth';

interface CoreApiStatusIndicatorProps {
  status: BackendHealthStatus['coreApi'];
  message?: string;
  checks?: BackendHealthStatus['coreApiChecks'];
}

const getStatusColor = (status: BackendHealthStatus['coreApi']) => {
  switch (status) {
    case 'healthy':
      return 'green';
    case 'degraded':
      return 'yellow';
    case 'unavailable':
      return 'red';
    default:
      return 'gray';
  }
};

const getStatusText = (status: BackendHealthStatus['coreApi']) => {
  switch (status) {
    case 'healthy':
      return 'Connected';
    case 'degraded':
      return 'Degraded';
    case 'unavailable':
      return 'Unavailable';
    default:
      return 'Unknown';
  }
};

const getStatusIcon = (status: BackendHealthStatus['coreApi']) => {
  switch (status) {
    case 'healthy':
      return IconCheck;
    case 'degraded':
      return IconAlertTriangle;
    case 'unavailable':
      return IconX;
    default:
      return IconServer;
  }
};

export function CoreApiStatusIndicator({ status, message, checks }: CoreApiStatusIndicatorProps) {
  const router = useRouter();
  const color = getStatusColor(status);
  const statusText = getStatusText(status);
  const StatusIcon = getStatusIcon(status);
  
  // Check if degraded due to no providers
  const isNoProvidersIssue = message?.toLowerCase().includes('no enabled providers') || 
                            message?.toLowerCase().includes('no providers');
  
  // Create detailed tooltip content
  const tooltipContent = (
    <Stack gap="xs">
      <Text size="sm" fw={600}>Core API: {statusText}</Text>
      
      {message && (
        <Text size="xs" c="dimmed">{message}</Text>
      )}
      
      {status === 'degraded' && isNoProvidersIssue && (
        <>
          <Text size="xs" c="yellow" fw={500}>
            ⚠️ Action Required
          </Text>
          <Text size="xs">
            No LLM providers are configured. The Core API needs at least one provider to function properly.
          </Text>
          <Button
            size="xs"
            variant="light"
            color="yellow"
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              router.push('/llm-providers');
            }}
          >
            Configure Providers
          </Button>
        </>
      )}
      
      {/* Show individual health checks if available */}
      {checks && checks.length > 0 && (
        <Stack gap={4} mt="xs">
          <Text size="xs" fw={500}>Health Checks:</Text>
          {checks.map((check) => (
            <Text key={check.name} size="xs" c="dimmed">
              • {check.name}: {check.status}
              {check.description && ` - ${check.description}`}
            </Text>
          ))}
        </Stack>
      )}
    </Stack>
  );
  
  // For degraded state with no providers, show a more prominent indicator
  if (status === 'degraded' && isNoProvidersIssue) {
    return (
      <Tooltip 
        label={tooltipContent} 
        position="bottom"
        multiline
        w={300}
        withArrow
      >
        <Indicator 
          color={color} 
          size={10} 
          position="top-end"
          processing
          withBorder
        >
          <ThemeIcon 
            size="sm" 
            variant="filled" 
            color={color}
            style={{ cursor: 'pointer' }}
            onClick={() => router.push('/llm-providers')}
          >
            <StatusIcon size={14} />
          </ThemeIcon>
        </Indicator>
      </Tooltip>
    );
  }
  
  // Standard indicator for other states
  return (
    <Tooltip 
      label={tooltipContent} 
      position="bottom"
      multiline={!!message || (checks && checks.length > 0)}
      w={message || (checks && checks.length > 0) ? 300 : 'auto'}
    >
      <Indicator 
        color={color} 
        size={8} 
        position="top-end"
        processing={status === 'unavailable'}
      >
        <ThemeIcon 
          size="sm" 
          variant="light" 
          color={color}
        >
          <IconServer size={14} />
        </ThemeIcon>
      </Indicator>
    </Tooltip>
  );
}