'use client';

import {
  Card,
  Group,
  Text,
  Switch,
  NumberInput,
  TextInput,
  Button,
  Stack,
  Divider,
  Badge,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import { IconEdit, IconCheck, IconX } from '@tabler/icons-react';
import { useState } from 'react';

interface SettingsCardProps {
  title: string;
  description?: string;
  category?: string;
  children: React.ReactNode;
  onSave?: () => void;
  isEditing?: boolean;
  onToggleEdit?: () => void;
  isDirty?: boolean;
}

export function SettingsCard({
  title,
  description,
  category,
  children,
  onSave,
  isEditing = false,
  onToggleEdit,
  isDirty = false,
}: SettingsCardProps) {
  return (
    <Card withBorder>
      <Stack gap="md">
        <Group justify="space-between">
          <div>
            <Group gap="xs" mb={4}>
              <Text fw={600}>{title}</Text>
              {category && (
                <Badge size="xs" variant="light" color="blue">
                  {category}
                </Badge>
              )}
            </Group>
            {description && (
              <Text size="sm" c="dimmed">
                {description}
              </Text>
            )}
          </div>

          {onToggleEdit && (
            <Group gap="xs">
              {isEditing && isDirty && (
                <>
                  <Tooltip label="Save changes">
                    <ActionIcon
                      color="green"
                      variant="light"
                      onClick={onSave}
                    >
                      <IconCheck size={16} />
                    </ActionIcon>
                  </Tooltip>
                  <Tooltip label="Cancel">
                    <ActionIcon
                      color="red"
                      variant="light"
                      onClick={onToggleEdit}
                    >
                      <IconX size={16} />
                    </ActionIcon>
                  </Tooltip>
                </>
              )}
              {!isEditing && (
                <Tooltip label="Edit settings">
                  <ActionIcon
                    variant="subtle"
                    onClick={onToggleEdit}
                  >
                    <IconEdit size={16} />
                  </ActionIcon>
                </Tooltip>
              )}
            </Group>
          )}
        </Group>

        <Divider />

        <div>
          {children}
        </div>
      </Stack>
    </Card>
  );
}

interface SettingRowProps {
  label: string;
  description?: string;
  children: React.ReactNode;
  required?: boolean;
}

export function SettingRow({ label, description, children, required }: SettingRowProps) {
  return (
    <Group justify="space-between" wrap="nowrap">
      <div style={{ flex: 1 }}>
        <Text size="sm" fw={500}>
          {label}
          {required && <Text component="span" c="red"> *</Text>}
        </Text>
        {description && (
          <Text size="xs" c="dimmed" mt={2}>
            {description}
          </Text>
        )}
      </div>
      <div style={{ minWidth: 200 }}>
        {children}
      </div>
    </Group>
  );
}