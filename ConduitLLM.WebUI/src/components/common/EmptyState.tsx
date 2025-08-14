import { Stack, Text, Title, ThemeIcon, Button } from '@mantine/core';
import { IconDatabaseOff, IconPlus } from '@tabler/icons-react';
import React from 'react';

interface EmptyStateProps {
  icon?: React.ReactNode;
  title: string;
  description: string;
  action?: {
    label: string;
    onClick: () => void;
    icon?: React.ReactNode;
  };
}

export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <Stack align="center" gap="md" py="xl">
      {icon && (
        <ThemeIcon size={64} variant="light" color="gray">
          {icon}
        </ThemeIcon>
      )}
      <Stack gap="xs" align="center" ta="center" maw={400}>
        <Title order={3} c="dimmed">
          {title}
        </Title>
        <Text c="dimmed" size="sm">
          {description}
        </Text>
      </Stack>
      {action && (
        <Button
          variant="light"
          leftSection={action.icon ?? <IconPlus size={16} />}
          onClick={action.onClick}
        >
          {action.label}
        </Button>
      )}
    </Stack>
  );
}

interface TableEmptyStateProps {
  colSpan: number;
  title?: string;
  description?: string;
  icon?: React.ReactNode;
}

export function TableEmptyState({ 
  colSpan, 
  title = 'No data found',
  description = 'There are no items to display at this time.',
  icon = <IconDatabaseOff size={24} />
}: TableEmptyStateProps) {
  return (
    <tr>
      <td colSpan={colSpan} style={{ textAlign: 'center', padding: '3rem 1rem' }}>
        <Stack align="center" gap="xs">
          <ThemeIcon size={48} variant="light" color="gray">
            {icon}
          </ThemeIcon>
          <Text fw={500} c="dimmed">
            {title}
          </Text>
          <Text size="sm" c="dimmed">
            {description}
          </Text>
        </Stack>
      </td>
    </tr>
  );
}