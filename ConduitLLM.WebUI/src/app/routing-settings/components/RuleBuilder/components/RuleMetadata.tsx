'use client';

import { useState } from 'react';
import {
  Card,
  Stack,
  TextInput,
  Textarea,
  NumberInput,
  Switch,
  Group,
  Text,
  Title,
  Tooltip,
  ActionIcon,
} from '@mantine/core';
import { IconInfoCircle } from '@tabler/icons-react';
import { CreateRoutingRuleRequest } from '../../../types/routing';

interface RuleMetadataProps {
  formData: CreateRoutingRuleRequest;
  onUpdate: (updates: Partial<CreateRoutingRuleRequest>) => void;
  errors: string[];
}

export function RuleMetadata({ formData, onUpdate, errors }: RuleMetadataProps) {
  const [nameError, setNameError] = useState<string | null>(null);

  const validateName = (name: string) => {
    if (!name.trim()) {
      setNameError('Rule name is required');
      return false;
    }
    if (name.length < 3) {
      setNameError('Rule name must be at least 3 characters');
      return false;
    }
    if (name.length > 100) {
      setNameError('Rule name must be less than 100 characters');
      return false;
    }
    setNameError(null);
    return true;
  };

  const handleNameChange = (value: string) => {
    validateName(value);
    onUpdate({ name: value });
  };

  const hasNameError = errors.some(error => error.includes('name')) || nameError;

  return (
    <Card withBorder p="md">
      <Group justify="space-between" mb="md">
        <Title order={5}>Rule Details</Title>
        <Tooltip 
          label="Basic information about the routing rule"
          position="left"
        >
          <ActionIcon variant="subtle" size="sm">
            <IconInfoCircle size={14} />
          </ActionIcon>
        </Tooltip>
      </Group>

      <Stack gap="md">
        <TextInput
          label="Rule Name"
          placeholder="Enter a descriptive name for this rule"
          value={formData.name}
          onChange={(e) => handleNameChange(e.target.value)}
          error={nameError}
          required
          withAsterisk
          autoComplete="off"
          data-autofocus
        />

        <Textarea
          label="Description"
          placeholder="Describe what this rule does and when it should be applied"
          value={formData.description ?? ''}
          onChange={(e) => onUpdate({ description: e.target.value })}
          autosize
          minRows={2}
          maxRows={4}
        />

        <Group grow>
          <div>
            <NumberInput
              label="Priority"
              placeholder="Rule priority (1-1000)"
              value={formData.priority}
              onChange={(value) => onUpdate({ priority: Number(value) || 10 })}
              min={1}
              max={1000}
              required
              withAsterisk
              description="Lower numbers = higher priority"
            />
          </div>
          
          <div>
            <Text size="sm" fw={500} mb="xs">
              Rule Status
            </Text>
            <Switch
              label="Enable this rule"
              description="Disabled rules will not be evaluated"
              checked={formData.enabled}
              onChange={(e) => onUpdate({ enabled: e.target.checked })}
              color="green"
            />
          </div>
        </Group>

        {/* Rule Health Indicator */}
        <Card withBorder p="sm" bg="gray.0">
          <Group gap="xs">
            <div
              style={{
                width: 8,
                height: 8,
                borderRadius: '50%',
                backgroundColor: formData.name && !hasNameError ? '#51cf66' : '#ffa8a8',
              }}
            />
            <Text size="xs" c="dimmed">
              {formData.name && !hasNameError 
                ? 'Rule metadata is valid' 
                : 'Complete rule metadata to continue'
              }
            </Text>
          </Group>
        </Card>
      </Stack>
    </Card>
  );
}