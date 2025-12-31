'use client';

import { Container, Title, Text, Stack } from '@mantine/core';
import MediaCleanupStatusContent from './MediaCleanupStatusContent';

export default function MediaCleanupStatusPage() {
  return (
    <Container size="xl" py="xl">
      <Stack gap="xl">
        <div>
          <Title order={1}>Media Cleanup Service</Title>
          <Text c="dimmed" mt="xs">
            Monitor and control the automatic media cleanup background service
          </Text>
        </div>
        <MediaCleanupStatusContent />
      </Stack>
    </Container>
  );
}
