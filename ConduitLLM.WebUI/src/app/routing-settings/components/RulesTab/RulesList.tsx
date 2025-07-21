'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Badge,
  ActionIcon,
  Menu,
  Switch,
  Tooltip,
} from '@mantine/core';
import {
  IconDots,
  IconEdit,
  IconTrash,
  IconEye,
  IconClock,
  IconTarget,
} from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import { RoutingRule } from '../../types/routing';

interface RulesListProps {
  rules: RoutingRule[];
  onEdit: (rule: RoutingRule) => void;
  onDelete: (id: string) => void;
  onToggle: (id: string, enabled: boolean) => void;
}

export function RulesList({ rules, onEdit, onDelete, onToggle }: RulesListProps) {
  const handleDeleteClick = (rule: RoutingRule) => {
    modals.openConfirmModal({
      title: 'Delete Routing Rule',
      children: (
        <Text size="sm">
          Are you sure you want to delete the rule &quot;{rule.name}&quot;? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => onDelete(rule.id),
    });
  };

  const getConditionsSummary = (rule: RoutingRule) => {
    if (rule.conditions.length === 0) return 'No conditions';
    if (rule.conditions.length === 1) {
      const condition = rule.conditions[0];
      const valueStr = Array.isArray(condition.value) 
        ? condition.value.join(', ') 
        : String(condition.value);
      return `${condition.type} ${condition.operator} ${valueStr}`;
    }
    return `${rule.conditions.length} conditions`;
  };

  const getActionsSummary = (rule: RoutingRule) => {
    if (rule.actions.length === 0) return 'No actions';
    if (rule.actions.length === 1) {
      const action = rule.actions[0];
      return action.target ? `${action.type} → ${action.target}` : action.type;
    }
    return `${rule.actions.length} actions`;
  };

  const formatLastMatched = (dateString?: string) => {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  return (
    <Stack gap="sm">
      {rules
        .sort((a, b) => b.priority - a.priority)
        .map((rule) => (
          <Card key={rule.id} shadow="sm" p="md" radius="md" withBorder>
            <Group justify="space-between" align="flex-start">
              <div style={{ flex: 1 }}>
                <Group align="center" gap="sm" mb="xs">
                  <Text fw={500} size="sm">
                    {rule.name}
                  </Text>
                  <Badge 
                    variant="light" 
                    color={rule.isEnabled ? 'green' : 'gray'}
                    size="xs"
                  >
                    {rule.isEnabled ? 'Active' : 'Disabled'}
                  </Badge>
                  <Badge variant="outline" size="xs">
                    Priority {rule.priority}
                  </Badge>
                </Group>

                {rule.description && (
                  <Text c="dimmed" size="xs" mb="xs">
                    {rule.description}
                  </Text>
                )}

                <Group gap="lg" mt="sm">
                  <Group gap="xs">
                    <IconTarget size={14} color="gray" />
                    <Text size="xs" c="dimmed">
                      {getConditionsSummary(rule)}
                    </Text>
                  </Group>
                  <Group gap="xs">
                    <IconEye size={14} color="gray" />
                    <Text size="xs" c="dimmed">
                      {getActionsSummary(rule)}
                    </Text>
                  </Group>
                  {rule.stats && (
                    <Group gap="xs">
                      <IconClock size={14} color="gray" />
                      <Text size="xs" c="dimmed">
                        {rule.stats.matchCount} matches
                      </Text>
                    </Group>
                  )}
                </Group>

                {rule.stats?.lastMatched && (
                  <Text size="xs" c="dimmed" mt="xs">
                    Last matched: {formatLastMatched(rule.stats.lastMatched)}
                  </Text>
                )}
              </div>

              <Group align="center" gap="xs">
                <Tooltip label={rule.isEnabled ? 'Disable rule' : 'Enable rule'}>
                  <Switch
                    checked={rule.isEnabled}
                    onChange={(event) => onToggle(rule.id, event.currentTarget.checked)}
                    size="sm"
                  />
                </Tooltip>

                <Menu shadow="md" width={200}>
                  <Menu.Target>
                    <ActionIcon variant="subtle" color="gray">
                      <IconDots size={16} />
                    </ActionIcon>
                  </Menu.Target>

                  <Menu.Dropdown>
                    <Menu.Item
                      leftSection={<IconEdit size={14} />}
                      onClick={() => onEdit(rule)}
                    >
                      Edit Rule
                    </Menu.Item>
                    <Menu.Item
                      leftSection={<IconTrash size={14} />}
                      color="red"
                      onClick={() => handleDeleteClick(rule)}
                    >
                      Delete Rule
                    </Menu.Item>
                  </Menu.Dropdown>
                </Menu>
              </Group>
            </Group>
          </Card>
        ))}
    </Stack>
  );
}