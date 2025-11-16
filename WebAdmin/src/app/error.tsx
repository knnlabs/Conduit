'use client';

import { useEffect } from 'react';
import { Container, Title, Text, Button, Stack, Code, Paper, Group } from '@mantine/core';
import { IconAlertTriangle, IconRefresh, IconHome } from '@tabler/icons-react';
import { useRouter } from 'next/navigation';
import { safeLog } from '@/lib/utils/logging';

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const router = useRouter();

  useEffect(() => {
    // Log the error to console and any error reporting service
    safeLog('Application error occurred', {
      error: error.message,
      stack: error.stack,
      digest: error.digest,
    });
  }, [error]);

  return (
    <Container size="sm" py="xl">
      <Paper p="xl" withBorder>
        <Stack gap="md" align="center" ta="center">
          <IconAlertTriangle size={64} color="var(--mantine-color-red-6)" />
          
          <div>
            <Title order={2} mb="xs">Something went wrong!</Title>
            <Text c="dimmed" size="lg">
              An unexpected error occurred while processing your request.
            </Text>
          </div>

          {/* ALWAYS SHOW ERROR DETAILS */}
          <Code block p="md" style={{ width: '100%', textAlign: 'left', maxHeight: '400px', overflow: 'auto' }}>
            ERROR MESSAGE: {error.message}
            {error.stack && (
              <>
                {'\n\n'}
                STACK TRACE:
                {'\n'}
                {error.stack}
              </>
            )}
            {'\n\n'}
            ERROR OBJECT: {JSON.stringify(error, null, 2)}
          </Code>

          <Group>
            <Button
              leftSection={<IconRefresh size={16} />}
              onClick={reset}
              variant="filled"
            >
              Try Again
            </Button>
            <Button
              leftSection={<IconHome size={16} />}
              onClick={() => router.push('/')}
              variant="light"
            >
              Go Home
            </Button>
          </Group>

          <Text size="sm" c="dimmed">
            If this problem persists, please contact support.
            {error.digest && (
              <>
                {' '}Error ID: <Code>{error.digest}</Code>
              </>
            )}
          </Text>
        </Stack>
      </Paper>
    </Container>
  );
}