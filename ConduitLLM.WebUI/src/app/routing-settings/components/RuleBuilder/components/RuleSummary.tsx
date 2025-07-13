'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  Badge,
  Code,
  Divider,
  ActionIcon,
  Tooltip,
  Collapse,
} from '@mantine/core';
import { IconEye, IconEyeOff, IconCode } from '@tabler/icons-react';
import { useState } from 'react';
import { CreateRoutingRuleRequest } from '../../../types/routing';

interface RuleSummaryProps {
  rule: CreateRoutingRuleRequest;
}

export function RuleSummary({ rule }: RuleSummaryProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [showJson, setShowJson] = useState(false);

  const formatConditionText = (condition: any, index: number) => {
    const { type, field, operator, value } = condition;
    
    let conditionText = '';
    
    // Add field context
    if (field && (type === 'header' || type === 'metadata')) {
      conditionText = `${type}.${field}`;
    } else {
      conditionText = type;
    }
    
    // Add operator and value
    conditionText += ` ${operator} `;
    
    if (operator === 'in_list') {
      conditionText += `[${value}]`;
    } else if (operator === 'exists') {
      conditionText += '(exists)';
    } else {
      conditionText += `"${value}"`;
    }
    
    return conditionText;
  };

  const formatActionText = (action: any) => {
    const { type, target, parameters } = action;
    
    let actionText = type;
    
    if (target) {
      actionText += ` → ${target}`;
    }
    
    if (parameters && Object.keys(parameters).length > 0) {
      const paramTexts = Object.entries(parameters)
        .filter(([_, value]) => value !== undefined && value !== '')
        .map(([key, value]) => `${key}: ${value}`);
      
      if (paramTexts.length > 0) {
        actionText += ` (${paramTexts.join(', ')})`;
      }
    }
    
    return actionText;
  };

  const generateHumanReadableRule = () => {
    if (!rule.name) return 'Rule not configured yet';
    
    let ruleText = `When `;
    
    // Add conditions
    if (rule.conditions.length === 0) {
      ruleText += 'no conditions are met';
    } else if (rule.conditions.length === 1) {
      ruleText += formatConditionText(rule.conditions[0], 0);
    } else {
      ruleText += rule.conditions
        .map((condition, index) => formatConditionText(condition, index))
        .join(' AND '); // TODO: Use actual logical operators
    }
    
    ruleText += `, then `;
    
    // Add actions
    if (rule.actions.length === 0) {
      ruleText += 'do nothing';
    } else if (rule.actions.length === 1) {
      ruleText += formatActionText(rule.actions[0]);
    } else {
      ruleText += rule.actions
        .map(action => formatActionText(action))
        .join(', then ');
    }
    
    return ruleText;
  };

  const isRuleComplete = rule.name && rule.conditions.length > 0 && rule.actions.length > 0;

  return (
    <Card withBorder p="md">
      <Group justify="space-between" mb="md">
        <Group gap="xs">
          <Title order={5}>Rule Preview</Title>
          <Badge 
            variant="light" 
            color={isRuleComplete ? 'green' : 'orange'}
            size="sm"
          >
            {isRuleComplete ? 'Complete' : 'Incomplete'}
          </Badge>
        </Group>
        
        <Group gap="xs">
          <Tooltip label={showJson ? 'Show human-readable' : 'Show JSON'}>
            <ActionIcon
              variant="subtle"
              onClick={() => setShowJson(!showJson)}
            >
              <IconCode size={16} />
            </ActionIcon>
          </Tooltip>
          <Tooltip label={isExpanded ? 'Collapse' : 'Expand'}>
            <ActionIcon
              variant="subtle"
              onClick={() => setIsExpanded(!isExpanded)}
            >
              {isExpanded ? <IconEyeOff size={16} /> : <IconEye size={16} />}
            </ActionIcon>
          </Tooltip>
        </Group>
      </Group>

      {showJson ? (
        <Code block style={{ fontSize: 12, maxHeight: 300, overflow: 'auto' }}>
          {JSON.stringify(rule, null, 2)}
        </Code>
      ) : (
        <Stack gap="sm">
          {/* Human-readable rule */}
          <Card withBorder p="sm" bg="blue.0">
            <Text size="sm" style={{ fontStyle: 'italic' }}>
              "{generateHumanReadableRule()}"
            </Text>
          </Card>

          <Collapse in={isExpanded}>
            <Stack gap="md">
              <Divider />
              
              {/* Rule Details */}
              <Group grow>
                <div>
                  <Text size="xs" fw={500} c="dimmed" mb={4}>
                    PRIORITY
                  </Text>
                  <Badge variant="outline" size="sm">
                    {rule.priority || 10}
                  </Badge>
                </div>
                <div>
                  <Text size="xs" fw={500} c="dimmed" mb={4}>
                    STATUS
                  </Text>
                  <Badge 
                    variant="outline" 
                    size="sm"
                    color={rule.enabled ? 'green' : 'red'}
                  >
                    {rule.enabled ? 'Enabled' : 'Disabled'}
                  </Badge>
                </div>
                <div>
                  <Text size="xs" fw={500} c="dimmed" mb={4}>
                    CONDITIONS
                  </Text>
                  <Badge variant="outline" size="sm">
                    {rule.conditions.length}
                  </Badge>
                </div>
                <div>
                  <Text size="xs" fw={500} c="dimmed" mb={4}>
                    ACTIONS
                  </Text>
                  <Badge variant="outline" size="sm">
                    {rule.actions.length}
                  </Badge>
                </div>
              </Group>

              {/* Conditions List */}
              {rule.conditions.length > 0 && (
                <div>
                  <Text size="xs" fw={500} c="dimmed" mb="xs">
                    CONDITIONS:
                  </Text>
                  <Stack gap={4}>
                    {rule.conditions.map((condition, index) => (
                      <Text key={index} size="sm" c="dark.6">
                        {index + 1}. {formatConditionText(condition, index)}
                      </Text>
                    ))}
                  </Stack>
                </div>
              )}

              {/* Actions List */}
              {rule.actions.length > 0 && (
                <div>
                  <Text size="xs" fw={500} c="dimmed" mb="xs">
                    ACTIONS:
                  </Text>
                  <Stack gap={4}>
                    {rule.actions.map((action, index) => (
                      <Text key={index} size="sm" c="dark.6">
                        {index + 1}. {formatActionText(action)}
                      </Text>
                    ))}
                  </Stack>
                </div>
              )}
            </Stack>
          </Collapse>
        </Stack>
      )}
    </Card>
  );
}