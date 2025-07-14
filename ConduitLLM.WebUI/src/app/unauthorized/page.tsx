import { Title, Text, Button, Container, Stack } from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import Link from 'next/link';

export default function UnauthorizedPage() {
  return (
    <Container size="sm" py="xl">
      <Stack align="center" gap="lg">
        <IconLock size={64} color="red" />
        <Title order={1}>Access Denied</Title>
        <Text size="lg" ta="center">
          You don't have permission to access this administration portal.
        </Text>
        <Text size="md" ta="center" c="dimmed">
          This area is restricted to authorized administrators only. 
          If you believe you should have access, please contact your system administrator.
        </Text>
        <Button component={Link} href="/sign-in" variant="subtle">
          Sign in with a different account
        </Button>
      </Stack>
    </Container>
  );
}