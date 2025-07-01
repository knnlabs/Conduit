'use client';

import { Badge } from '@mantine/core';
import { IconActivity, IconCircleX } from '@tabler/icons-react';
import { useConnectionStore } from '@/stores/useConnectionStore';

export function RealTimeStatus() {
  const { status } = useConnectionStore();
  const signalRStatus = status.signalR;
  
  const isConnected = signalRStatus === 'connected';
  
  return (
    <Badge
      size="sm"
      color={isConnected ? 'green' : 'gray'}
      variant="light"
      leftSection={
        isConnected ? 
          <IconActivity size={12} className="animate-pulse" /> : 
          <IconCircleX size={12} />
      }
    >
      {isConnected ? 'Live' : 'Offline'}
    </Badge>
  );
}