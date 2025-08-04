'use client';

import { Group } from '@mantine/core';
import { useBackendHealth } from '@/hooks/useBackendHealth';
import { CoreApiStatusIndicator } from './CoreApiStatusIndicator';
import { AdminApiStatusIndicator } from './AdminApiStatusIndicator';

export function ConnectionIndicator() {
  const { health } = useBackendHealth();

  if (!health) {
    return (
      <Group gap="xs">
        <CoreApiStatusIndicator
          status="unavailable"
          message="Loading..."
          lastChecked={undefined}
        />
        
        <AdminApiStatusIndicator
          status="unavailable"
          lastChecked={undefined}
        />
      </Group>
    );
  }

  return (
    <Group gap="xs">
      <CoreApiStatusIndicator
        status={health.coreApi}
        message={health.coreApiMessage}
        checks={undefined}
        lastChecked={health.lastChecked}
      />
      
      <AdminApiStatusIndicator
        status={health.adminApi}
        checks={undefined}
        lastChecked={health.lastChecked}
      />
      
    </Group>
  );
}