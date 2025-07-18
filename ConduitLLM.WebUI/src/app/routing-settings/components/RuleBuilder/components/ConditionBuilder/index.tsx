'use client';

import { useState } from 'react';
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
  Select,
} from '@mantine/core';
import { IconPlus, IconInfoCircle } from '@tabler/icons-react';
import { RoutingCondition } from '../../../../types/routing';
import { ConditionRow } from './ConditionRow';

interface ConditionBuilderProps {
  conditions: Omit<RoutingCondition, 'logicalOperator'>[];
  onUpdate: (conditions: Omit<RoutingCondition, 'logicalOperator'>[]) => void;
  errors: string[];
}

export function ConditionBuilder({ conditions, onUpdate, errors }: ConditionBuilderProps) {
  const [logicalOperators, setLogicalOperators] = useState<('AND' | 'OR')[]>([]);

  const addCondition = () => {
    const newCondition: Omit<RoutingCondition, 'logicalOperator'> = {
      type: 'model',
      operator: 'equals',
      value: '',
    };

    onUpdate([...conditions, newCondition]);

    // Add logical operator for the new condition (except for the first one)
    if (conditions.length > 0) {
      setLogicalOperators([...logicalOperators, 'AND']);
    }
  };

  const updateCondition = (index: number, updates: Partial<RoutingCondition>) => {
    const updatedConditions = conditions.map((condition, i) =>
      i === index ? { ...condition, ...updates } : condition
    );
    onUpdate(updatedConditions);
  };

  const removeCondition = (index: number) => {
    const updatedConditions = conditions.filter((unusedItem, i) => {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const unused = unusedItem;
      return i !== index;
    });
    onUpdate(updatedConditions);

    // Remove the corresponding logical operator
    if (index > 0) {
      // Remove the operator before this condition
      setLogicalOperators(logicalOperators.filter((unusedOp, i) => {
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const unused = unusedOp;
        return i !== index - 1;
      }));
    } else if (logicalOperators.length > 0) {
      // Remove the first operator if removing the first condition
      setLogicalOperators(logicalOperators.slice(1));
    }
  };

  const updateLogicalOperator = (index: number, operator: 'AND' | 'OR') => {
    const updatedOperators = [...logicalOperators];
    updatedOperators[index] = operator;
    setLogicalOperators(updatedOperators);
  };

  const hasConditionErrors = errors.some(error => 
    error.includes('condition') || error.includes('At least one condition')
  );

  return (
    <Card withBorder p="md">
      <Group justify="space-between" mb="md">
        <Group gap="xs">
          <Title order={5}>Conditions</Title>
          <Badge variant="light" size="sm">
            {conditions.length}
          </Badge>
          <Tooltip 
            label="Define the criteria that trigger this rule. Multiple conditions can be combined with AND/OR logic."
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
          onClick={addCondition}
        >
          Add Condition
        </Button>
      </Group>

      <Stack gap="md">
        {conditions.length === 0 ? (
          <Card withBorder p="xl" bg="gray.0">
            <Stack align="center" gap="sm">
              <Text c="dimmed" ta="center">
                No conditions defined yet
              </Text>
              <Text size="sm" c="dimmed" ta="center">
                Click &quot;Add Condition&quot; to define when this rule should be triggered
              </Text>
              <Button
                leftSection={<IconPlus size={16} />}
                variant="outline"
                size="sm"
                onClick={addCondition}
              >
                Add First Condition
              </Button>
            </Stack>
          </Card>
        ) : (
          conditions.map((condition, index) => (
            <div key={`condition-${condition.type}-${index}`}>
              <ConditionRow
                condition={condition}
                index={index}
                onUpdate={(updates) => updateCondition(index, updates)}
                onRemove={() => removeCondition(index)}
                canRemove={conditions.length > 1}
              />
              
              {/* Logical Operator Selector (between conditions) */}
              {index < conditions.length - 1 && (
                <Group justify="center" my="sm">
                  <Select
                    data={[
                      { value: 'AND', label: 'AND' },
                      { value: 'OR', label: 'OR' },
                    ]}
                    value={logicalOperators[index] || 'AND'}
                    onChange={(value) => updateLogicalOperator(index, value as 'AND' | 'OR')}
                    w={80}
                    variant="filled"
                    size="xs"
                  />
                </Group>
              )}
            </div>
          ))
        )}

        {/* Condition Summary */}
        {conditions.length > 1 && (
          <Card withBorder p="sm" bg="blue.0">
            <Text size="sm" fw={500} mb="xs">
              Logic Summary:
            </Text>
            <Text size="sm" c="dimmed">
              {conditions.map((condition, index) => {
                const conditionText = `${condition.type} ${condition.operator} &quot;${Array.isArray(condition.value) ? condition.value.join(', ') : String(condition.value)}&quot;`;
                const operator = index < logicalOperators.length ? ` ${logicalOperators[index]} ` : '';
                return conditionText + operator;
              }).join('')}
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
                backgroundColor: conditions.length > 0 && !hasConditionErrors ? '#51cf66' : '#ffa8a8',
              }}
            />
            <Text size="xs" c="dimmed">
              {conditions.length > 0 && !hasConditionErrors
                ? `${conditions.length} condition${conditions.length > 1 ? 's' : ''} configured`
                : 'At least one condition is required'
              }
            </Text>
          </Group>
        </Card>
      </Stack>
    </Card>
  );
}