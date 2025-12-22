import dynamic from 'next/dynamic';
import { Skeleton, Stack } from '@mantine/core';

const SecurityDashboard = dynamic(() => import('./SecurityDashboard'), {
  loading: () => (
    <Stack gap="lg" p="md">
      <Skeleton height={60} radius="md" />
      <Skeleton height={120} radius="md" />
      <Skeleton height={100} radius="md" />
      <Skeleton height={400} radius="md" />
    </Stack>
  ),
});

export default SecurityDashboard;
