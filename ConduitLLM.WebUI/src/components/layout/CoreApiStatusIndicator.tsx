'use client';

import { Indicator, ThemeIcon } from '@mantine/core';
import { IconServer } from '@tabler/icons-react';
import { StatusHoverCard } from '@/components/common/StatusHoverCard';
import { BackendHealthStatus } from '@/hooks/useBackendHealth';
import type { HealthCheckDetail } from '@/types/health';

interface CoreApiStatusIndicatorProps {
  status: BackendHealthStatus['coreApi'];
  message?: string;
  checks?: Record<string, HealthCheckDetail>;
  lastChecked?: Date;
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

export function CoreApiStatusIndicator({ status, message, checks, lastChecked }: CoreApiStatusIndicatorProps) {
  const color = getStatusColor(status);
  
  return (
    <StatusHoverCard
      status={status}
      title="Core API"
      lastChecked={lastChecked}
      checks={checks}
      message={message}
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
    </StatusHoverCard>
  );
}