'use client';

import {
  Stack,
  Group,
  Text,
  Alert,
  Card,
  Divider,
} from '@mantine/core';
import { IconAlertCircle, IconInfoCircle } from '@tabler/icons-react';

// Form section divider with label
export interface FormSectionProps {
  title: string;
  description?: string;
  children: React.ReactNode;
  collapsible?: boolean;
  defaultCollapsed?: boolean;
}

export function FormSection({
  title,
  description,
  children,
}: FormSectionProps) {
  return (
    <Stack gap="md">
      <Divider
        label={
          <Group gap="xs">
            <Text fw={500}>{title}</Text>
          </Group>
        }
        labelPosition="left"
      />
      {description && (
        <Text size="sm" c="dimmed">
          {description}
        </Text>
      )}
      {children}
    </Stack>
  );
}

// Info alert component for form guidance
export interface FormInfoAlertProps {
  title?: string;
  message: string;
  type?: 'info' | 'warning' | 'error';
  variant?: 'light' | 'filled' | 'outline';
}

export function FormInfoAlert({
  title,
  message,
  type = 'info',
  variant = 'light',
}: FormInfoAlertProps) {
  const colors = {
    info: 'blue',
    warning: 'orange',
    error: 'red',
  };

  const icons = {
    info: <IconInfoCircle size={16} />,
    warning: <IconAlertCircle size={16} />,
    error: <IconAlertCircle size={16} />,
  };

  return (
    <Alert
      icon={icons[type]}
      title={title}
      color={colors[type]}
      variant={variant}
    >
      {message}
    </Alert>
  );
}

// Form card container for grouping related fields
export interface FormCardProps {
  title?: string;
  description?: string;
  children: React.ReactNode;
  withBorder?: boolean;
  padding?: 'xs' | 'sm' | 'md' | 'lg';
}

export function FormCard({
  title,
  description,
  children,
  withBorder = true,
  padding = 'md',
}: FormCardProps) {
  return (
    <Card withBorder={withBorder} padding={padding}>
      {(title ?? description) && (
        <Card.Section inheritPadding py="sm" withBorder>
          {title && <Text fw={500}>{title}</Text>}
          {description && <Text size="sm" c="dimmed">{description}</Text>}
        </Card.Section>
      )}
      <Card.Section inheritPadding py="sm">
        {children}
      </Card.Section>
    </Card>
  );
}