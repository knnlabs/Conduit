'use client';

import { Container, Title, Text, Stack } from '@mantine/core';
import MediaAssetsContent from './components/MediaAssetsContent';

export default function MediaAssetsPage() {
  return (
    <Container size="xl" py="xl">
      <Stack gap="xl">
        <div>
          <Title order={1}>Media Assets Management</Title>
          <Text c="dimmed" mt="xs">
            View and manage all generated images and videos across your organization
          </Text>
        </div>
        <MediaAssetsContent />
      </Stack>
    </Container>
  );
}