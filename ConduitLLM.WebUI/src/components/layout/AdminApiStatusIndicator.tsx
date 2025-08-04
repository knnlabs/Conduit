'use client';

import { Indicator, ThemeIcon } from '@mantine/core';
import { IconServer } from '@tabler/icons-react';
import { StatusHoverCard } from '@/components/common/StatusHoverCard';
import type { HealthCheckDetail } from '@/types/health';

interface AdminApiStatusIndicatorProps {
  status: 'healthy' | 'degraded' | 'unavailable';
  message?: string;
  checks?: Record<string, HealthCheckDetail>;
  lastChecked?: Date;
}

const getStatusColor = (status: 'healthy' | 'degraded' | 'unavailable') => {
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

export function AdminApiStatusIndicator({ status, message, checks, lastChecked }: AdminApiStatusIndicatorProps) {
  const color = getStatusColor(status);
  
  return (
    <StatusHoverCard
      status={status}
      title="Admin API"
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