'use client';

import { Container, Title, Text, Stack } from '@mantine/core';
import RetentionPoliciesContent from './RetentionPoliciesContent';

export default function RetentionPoliciesPage() {
  return (
    <Container size="xl" py="xl">
      <Stack gap="xl">
        <div>
          <Title order={1}>Media Retention Policies</Title>
          <Text c="dimmed" mt="xs">
            Configure retention policies that determine how long media is kept based on account balance
          </Text>
        </div>
        <RetentionPoliciesContent />
      </Stack>
    </Container>
  );
}
