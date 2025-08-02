'use client';

import { Group } from '@mantine/core';
import { useBackendHealth } from '@/hooks/useBackendHealth';
import { CoreApiStatusIndicator } from './CoreApiStatusIndicator';
import { AdminApiStatusIndicator } from './AdminApiStatusIndicator';

export function ConnectionIndicator() {
  const { healthStatus } = useBackendHealth();

  return (
    <Group gap="xs">
      <CoreApiStatusIndicator
        status={healthStatus.coreApi}
        message={healthStatus.coreApiMessage}
        checks={healthStatus.coreApiChecks}
        lastChecked={healthStatus.lastChecked}
      />
      
      <AdminApiStatusIndicator
        status={healthStatus.adminApi}
        checks={healthStatus.adminApiChecks}
        lastChecked={healthStatus.lastChecked}
      />
      
    </Group>
  );
}