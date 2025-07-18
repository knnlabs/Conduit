'use client';

import { useState, useEffect } from 'react';
import {
  Modal,
  Stack,
  TextInput,
  Textarea,
  NumberInput,
  Select,
  Switch,
  Button,
  Group,
  Card,
  Text,
  ActionIcon,
  Divider,
  Title,
} from '@mantine/core';
import { IconPlus, IconTrash } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { RoutingRule, CreateRoutingRuleRequest, RoutingCondition, RoutingAction } from '../../types/routing';

interface RuleEditorProps {
  isOpen: boolean;
  rule?: RoutingRule | null;
  onClose: () => void;
  onSave: (rule: CreateRoutingRuleRequest) => void;
}

const conditionTypes = [
  { value: 'model', label: 'Model' },
  { value: 'header', label: 'Header' },
  { value: 'body', label: 'Body' },
  { value: 'time', label: 'Time' },
  { value: 'load', label: 'Load' },
  { value: 'key', label: 'Key' },
  { value: 'metadata', label: 'Metadata' },
];

const operators = [
  { value: 'equals', label: 'Equals' },
  { value: 'contains', label: 'Contains' },
  { value: 'greater_than', label: 'Greater Than' },
  { value: 'less_than', label: 'Less Than' },
  { value: 'between', label: 'Between' },
  { value: 'in_list', label: 'In List' },
  { value: 'regex', label: 'Regex' },
  { value: 'exists', label: 'Exists' },
];

const actionTypes = [
  { value: 'route', label: 'Route to Provider' },
  { value: 'transform', label: 'Transform Request' },
  { value: 'cache', label: 'Cache Response' },
  { value: 'rate_limit', label: 'Rate Limit' },
  { value: 'log', label: 'Log Event' },
  { value: 'block', label: 'Block Request' },
];

export function RuleEditor({ isOpen, rule, onClose, onSave }: RuleEditorProps) {
  const [formData, setFormData] = useState<CreateRoutingRuleRequest>({
    name: '',
    description: '',
    priority: 10,
    conditions: [],
    actions: [],
    enabled: true,
  });

  useEffect(() => {
    if (rule) {
      setFormData({
        name: rule.name,
        description: rule.description ?? '',
        priority: rule.priority,
        conditions: rule.conditions.map(c => ({ ...c, logicalOperator: undefined })),
        actions: rule.actions,
        enabled: rule.isEnabled,
      });
    } else {
      setFormData({
        name: '',
        description: '',
        priority: 10,
        conditions: [],
        actions: [],
        enabled: true,
      });
    }
  }, [rule, isOpen]);

  const handleSubmit = () => {
    if (!formData.name.trim()) {
      notifications.show({
        title: 'Validation Error',
        message: 'Rule name is required',
        color: 'red',
      });
      return;
    }

    if (formData.conditions.length === 0) {
      notifications.show({
        title: 'Validation Error',
        message: 'At least one condition is required',
        color: 'red',
      });
      return;
    }

    if (formData.actions.length === 0) {
      notifications.show({
        title: 'Validation Error',
        message: 'At least one action is required',
        color: 'red',
      });
      return;
    }

    onSave(formData);
  };

  const addCondition = () => {
    setFormData(prev => ({
      ...prev,
      conditions: [
        ...prev.conditions,
        {
          type: 'model',
          operator: 'equals',
          value: '',
        }
      ]
    }));
  };

  const updateCondition = (index: number, field: keyof RoutingCondition, value: number | boolean | string[] | number[] | RoutingCondition['operator'] | RoutingCondition['logicalOperator'] | RoutingCondition['type'] | RoutingCondition['field'] | RoutingCondition['value']) => {
    setFormData(prev => ({
      ...prev,
      conditions: prev.conditions.map((condition, i) =>
        i === index ? { ...condition, [field]: value } : condition
      )
    }));
  };

  const removeCondition = (index: number) => {
    setFormData(prev => ({
      ...prev,
      conditions: prev.conditions.filter((_, i) => i !== index)
    }));
  };

  const addAction = () => {
    setFormData(prev => ({
      ...prev,
      actions: [
        ...prev.actions,
        {
          type: 'route',
          target: '',
          parameters: {},
        }
      ]
    }));
  };

  const updateAction = (index: number, field: keyof RoutingAction, value: Record<string, unknown> | RoutingAction['type'] | RoutingAction['target'] | RoutingAction['parameters']) => {
    setFormData(prev => ({
      ...prev,
      actions: prev.actions.map((action, i) =>
        i === index ? { ...action, [field]: value } : action
      )
    }));
  };

  const removeAction = (index: number) => {
    setFormData(prev => ({
      ...prev,
      actions: prev.actions.filter((_, i) => i !== index)
    }));
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={rule ? 'Edit Routing Rule' : 'Create Routing Rule'}
      size="lg"
    >
      <Stack gap="md">
        {/* Basic Information */}
        <Card withBorder p="md">
          <Title order={5} mb="md">Basic Information</Title>
          <Stack gap="sm">
            <TextInput
              label="Rule Name"
              placeholder="Enter rule name"
              value={formData.name}
              onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
              required
            />
            <Textarea
              label="Description"
              placeholder="Optional description"
              value={formData.description}
              onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
            />
            <Group grow>
              <NumberInput
                label="Priority"
                placeholder="Rule priority"
                value={formData.priority}
                onChange={(value) => setFormData(prev => ({ ...prev, priority: Number(value) || 10 }))}
                min={1}
                max={100}
              />
              <div>
                <Text size="sm" fw={500} mb="xs">Status</Text>
                <Switch
                  label="Rule enabled"
                  checked={formData.enabled}
                  onChange={(e) => setFormData(prev => ({ ...prev, enabled: e.target.checked }))}
                />
              </div>
            </Group>
          </Stack>
        </Card>

        {/* Conditions */}
        <Card withBorder p="md">
          <Group justify="space-between" mb="md">
            <Title order={5}>Conditions</Title>
            <Button
              leftSection={<IconPlus size={16} />}
              variant="light"
              size="xs"
              onClick={addCondition}
            >
              Add Condition
            </Button>
          </Group>
          <Stack gap="sm">
            {formData.conditions.map((condition, index) => (
              <Card key={`condition-${condition.field || condition.type}-${index}`} withBorder p="sm">
                <Group align="flex-end">
                  <Select
                    label="Type"
                    data={conditionTypes}
                    value={condition.type}
                    onChange={(value) => value && updateCondition(index, 'type', value)}
                    style={{ flex: 1 }}
                  />
                  {(condition.type === 'header' || condition.type === 'metadata') && (
                    <TextInput
                      label="Field"
                      placeholder="Field name"
                      value={condition.field ?? ''}
                      onChange={(e) => updateCondition(index, 'field', e.target.value)}
                      style={{ flex: 1 }}
                    />
                  )}
                  <Select
                    label="Operator"
                    data={operators}
                    value={condition.operator}
                    onChange={(value) => value && updateCondition(index, 'operator', value)}
                    style={{ flex: 1 }}
                  />
                  <TextInput
                    label="Value"
                    placeholder="Condition value"
                    value={typeof condition.value === 'string' || typeof condition.value === 'number' ? String(condition.value) : ''}
                    onChange={(e) => updateCondition(index, 'value', e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <ActionIcon
                    color="red"
                    variant="subtle"
                    onClick={() => removeCondition(index)}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                </Group>
              </Card>
            ))}
            {formData.conditions.length === 0 && (
              <Text c="dimmed" ta="center" py="md">
                No conditions defined. Click &quot;Add Condition&quot; to get started.
              </Text>
            )}
          </Stack>
        </Card>

        {/* Actions */}
        <Card withBorder p="md">
          <Group justify="space-between" mb="md">
            <Title order={5}>Actions</Title>
            <Button
              leftSection={<IconPlus size={16} />}
              variant="light"
              size="xs"
              onClick={addAction}
            >
              Add Action
            </Button>
          </Group>
          <Stack gap="sm">
            {formData.actions.map((action, index) => (
              <Card key={`action-${action.type}-${index}`} withBorder p="sm">
                <Group align="flex-end">
                  <Select
                    label="Type"
                    data={actionTypes}
                    value={action.type}
                    onChange={(value) => value && updateAction(index, 'type', value)}
                    style={{ flex: 1 }}
                  />
                  {(action.type === 'route' || action.type === 'transform') && (
                    <TextInput
                      label="Target"
                      placeholder="Provider or target"
                      value={action.target ?? ''}
                      onChange={(e) => updateAction(index, 'target', e.target.value)}
                      style={{ flex: 1 }}
                    />
                  )}
                  <ActionIcon
                    color="red"
                    variant="subtle"
                    onClick={() => removeAction(index)}
                  >
                    <IconTrash size={16} />
                  </ActionIcon>
                </Group>
              </Card>
            ))}
            {formData.actions.length === 0 && (
              <Text c="dimmed" ta="center" py="md">
                No actions defined. Click &quot;Add Action&quot; to get started.
              </Text>
            )}
          </Stack>
        </Card>

        <Divider />

        {/* Actions */}
        <Group justify="flex-end">
          <Button variant="subtle" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleSubmit}>
            {rule ? 'Update Rule' : 'Create Rule'}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}