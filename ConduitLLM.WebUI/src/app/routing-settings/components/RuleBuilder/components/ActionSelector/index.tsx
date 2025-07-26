'use client';

import React from 'react';
import {
  Card,
  Stack,
  Group,
  Button,
  Text,
  Title,
  Badge,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import { IconPlus, IconInfoCircle } from '@tabler/icons-react';
import { RoutingAction } from '../../../../types/routing';
import { ActionRow } from './ActionRow';

interface ActionSelectorProps {
  actions: RoutingAction[];
  onUpdate: (actions: RoutingAction[]) => void;
  errors: string[];
}

export function ActionSelector({ actions, onUpdate, errors }: ActionSelectorProps) {
  const addAction = () => {
    const newAction: RoutingAction = {
      type: 'route',
      target: '',
      parameters: {},
    };

    onUpdate([...actions, newAction]);
  };

  const updateAction = (index: number, updates: Partial<RoutingAction>) => {
    const updatedActions = actions.map((action, i) =>
      i === index ? { ...action, ...updates } : action
    );
    onUpdate(updatedActions);
  };

  const removeAction = (index: number) => {
    const updatedActions = actions.filter((unusedItem, i) => {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const unused = unusedItem;
      return i !== index;
    });
    onUpdate(updatedActions);
  };

  const hasActionErrors = errors.some(error => 
    error.includes('action') || error.includes('At least one action')
  );

  return (
    <Card withBorder p="md">
      <Group justify="space-between" mb="md">
        <Group gap="xs">
          <Title order={5}>Actions</Title>
          <Badge variant="light" size="sm">
            {actions.length}
          </Badge>
          <Tooltip 
            label="Define what happens when the conditions are met. Multiple actions can be configured to execute in sequence."
            position="right"
            multiline
            w={300}
          >
            <ActionIcon variant="subtle" size="sm">
              <IconInfoCircle size={14} />
            </ActionIcon>
          </Tooltip>
        </Group>
        
        <Button
          leftSection={<IconPlus size={16} />}
          variant="light"
          size="sm"
          onClick={addAction}
        >
          Add Action
        </Button>
      </Group>

      <Stack gap="md">
        {actions.length === 0 ? (
          <Card withBorder p="xl" bg="gray.0">
            <Stack align="center" gap="sm">
              <Text c="dimmed" ta="center">
                No actions defined yet
              </Text>
              <Text size="sm" c="dimmed" ta="center">
                Click &ldquo;Add Action&rdquo; to define what happens when this rule is triggered
              </Text>
              <Button
                leftSection={<IconPlus size={16} />}
                variant="outline"
                size="sm"
                onClick={addAction}
              >
                Add First Action
              </Button>
            </Stack>
          </Card>
        ) : (
          actions.map((action, index) => (
            <ActionRow
              key={`${action.type}-${action.target ?? ''}-${JSON.stringify(action.parameters ?? {})}`}
              action={action}
              index={index}
              onUpdate={(updates) => updateAction(index, updates)}
              onRemove={() => removeAction(index)}
              canRemove={actions.length > 1}
            />
          ))
        )}

        {/* Action Execution Order */}
        {actions.length > 1 && (
          <Card withBorder p="sm" bg="orange.0">
            <Text size="sm" fw={500} mb="xs">
              Execution Order:
            </Text>
            <Text size="sm" c="dimmed">
              Actions will be executed in the order shown above. You can drag to reorder them.
            </Text>
          </Card>
        )}

        {/* Validation Status */}
        <Card withBorder p="sm" bg="gray.0">
          <Group gap="xs">
            <div
              style={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                backgroundColor: actions.length > 0 && !hasActionErrors ? '#51cf66' : '#ffa8a8',
              }}
            />
            <Text size="xs" c="dimmed">
              {actions.length > 0 && !hasActionErrors
                ? `${actions.length} action${actions.length > 1 ? 's' : ''} configured`
                : 'At least one action is required'
              }
            </Text>
          </Group>
        </Card>
      </Stack>
    </Card>
  );
}