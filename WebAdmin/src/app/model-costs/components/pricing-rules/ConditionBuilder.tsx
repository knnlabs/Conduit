'use client';

import {
  Group,
  Select,
  TextInput,
  NumberInput,
  Switch,
  ActionIcon,
  Paper,
  Text,
  Badge,
} from '@mantine/core';
import { IconTrash, IconEqual } from '@tabler/icons-react';

interface ParameterDef {
  key: string;
  label: string;
  type: 'string' | 'number' | 'boolean' | 'enum';
  options?: string[];
}

interface ConditionBuilderProps {
  paramKey: string;
  value: string | number | boolean;
  parameterDef?: ParameterDef;
  onChange: (value: string | number | boolean) => void;
  onRemove: () => void;
  readOnly?: boolean;
}

/**
 * Builder for a single condition in a pricing rule
 */
export function ConditionBuilder({
  paramKey,
  value,
  parameterDef,
  onChange,
  onRemove,
  readOnly = false,
}: ConditionBuilderProps) {
  const getInferredType = (): 'string' | 'number' | 'boolean' | 'enum' => {
    if (typeof value === 'boolean') return 'boolean';
    if (typeof value === 'number') return 'number';
    return 'string';
  };
  const paramType = parameterDef?.type ?? getInferredType();
  const label = parameterDef?.label ?? paramKey;

  const renderValueInput = () => {
    switch (paramType) {
      case 'boolean':
        return (
          <Switch
            checked={value === true || value === 'true'}
            onChange={(e) => onChange(e.currentTarget.checked)}
            disabled={readOnly}
            label={value ? 'True' : 'False'}
          />
        );

      case 'number':
        return (
          <NumberInput
            value={typeof value === 'number' ? value : Number(value)}
            onChange={(v) => onChange(typeof v === 'number' ? v : 0)}
            disabled={readOnly}
            size="xs"
            style={{ width: 100 }}
          />
        );

      case 'enum':
        return (
          <Select
            value={String(value)}
            onChange={(v) => v && onChange(v)}
            data={parameterDef?.options?.map(o => ({ value: o, label: o })) ?? []}
            disabled={readOnly}
            size="xs"
            style={{ width: 120 }}
          />
        );

      default:
        return (
          <TextInput
            value={String(value)}
            onChange={(e) => onChange(e.target.value)}
            disabled={readOnly}
            size="xs"
            style={{ width: 120 }}
          />
        );
    }
  };

  return (
    <Paper withBorder p="xs" radius="sm">
      <Group gap="xs" wrap="nowrap">
        <Badge variant="light" color="blue" size="sm">
          {label}
        </Badge>

        <Group gap={4}>
          <IconEqual size={14} color="gray" />
          <Text size="xs" c="dimmed">equals</Text>
        </Group>

        {renderValueInput()}

        {!readOnly && (
          <ActionIcon
            variant="subtle"
            color="red"
            size="sm"
            onClick={onRemove}
            title="Remove condition"
          >
            <IconTrash size={14} />
          </ActionIcon>
        )}
      </Group>
    </Paper>
  );
}
