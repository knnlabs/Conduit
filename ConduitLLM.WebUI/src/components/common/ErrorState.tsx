import { Alert, Stack, Text, Button, Group } from '@mantine/core';
import { IconAlertCircle, IconRefresh } from '@tabler/icons-react';

interface ErrorStateProps {
  error: Error | string;
  title?: string;
  retry?: () => void;
  fullPage?: boolean;
}

export function ErrorState({ 
  error, 
  title = 'Error loading data', 
  retry,
  fullPage = false 
}: ErrorStateProps) {
  const errorMessage = typeof error === 'string' ? error : error.message;

  const content = (
    <Alert 
      icon={<IconAlertCircle size={16} />} 
      title={title} 
      color="red"
      variant="light"
    >
      <Stack gap="xs">
        <Text size="sm">{errorMessage}</Text>
        {retry && (
          <Group>
            <Button
              size="xs"
              variant="light"
              color="red"
              leftSection={<IconRefresh size={14} />}
              onClick={retry}
            >
              Try Again
            </Button>
          </Group>
        )}
      </Stack>
    </Alert>
  );

  if (fullPage) {
    return (
      <Stack h={400} justify="center" align="center">
        {content}
      </Stack>
    );
  }

  return content;
}