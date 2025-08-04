'use client';

import {
  Stack,
  Title,
  Text,
  Alert,
} from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';

export default function VirtualKeysDashboardPage() {
  return (
    <Stack gap="xl">
      <div>
        <Title order={1}>Virtual Keys Dashboard</Title>
        <Text c="dimmed">Analytics and insights for virtual key usage</Text>
      </div>

      <Alert 
        icon={<IconAlertCircle size={16} />} 
        title="Dashboard Under Reconstruction"
        color="yellow"
      >
        The Virtual Keys Dashboard is being updated to reflect our new group-based billing model. 
        Cost tracking and usage analytics are now managed at the Virtual Key Group level.
        A new dashboard will be available soon.
      </Alert>
    </Stack>
  );
}