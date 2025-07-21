'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  Badge,
  Alert,
  Accordion,
  Table,
  Tooltip,
  ThemeIcon,
} from '@mantine/core';
import {
  IconCheck,
  IconX,
  IconAlertTriangle,
  IconClock,
  IconTarget,
  IconListCheck,
} from '@tabler/icons-react';
import { TestResult, TestRequest, MatchedRule } from '../../../types/routing';

interface TestResultsProps {
  result: TestResult;
  request: TestRequest;
}

export function TestResults({ result, request }: TestResultsProps) {
  const getConditionIcon = (matched: boolean) => {
    return matched ? (
      <ThemeIcon size="sm" color="green" variant="light">
        <IconCheck size={12} />
      </ThemeIcon>
    ) : (
      <ThemeIcon size="sm" color="red" variant="light">
        <IconX size={12} />
      </ThemeIcon>
    );
  };

  const getSuccessColor = (success: boolean) => {
    return success ? 'green' : 'red';
  };

  const formatConditionValue = (condition: unknown, actualValue: unknown): string => {
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const unusedCondition = condition;
    if (actualValue === undefined || actualValue === null) {
      return 'N/A';
    }
    
    if (typeof actualValue === 'object') {
      return JSON.stringify(actualValue);
    }
    
    if (typeof actualValue === 'string' || typeof actualValue === 'number' || typeof actualValue === 'boolean') {
      return String(actualValue);
    }
    
    return JSON.stringify(actualValue);
  };

  const getRuleStatusBadge = (rule: MatchedRule) => {
    const allMatched = rule.matchedConditions.every(c => c.matched);
    
    if (rule.applied) {
      return <Badge color="green" variant="light">Applied</Badge>;
    } else if (allMatched) {
      return <Badge color="blue" variant="light">Matched</Badge>;
    } else {
      return <Badge color="gray" variant="light">Not Matched</Badge>;
    }
  };

  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Stack gap="md">
        {/* Header */}
        <Group justify="space-between" align="flex-start">
          <div>
            <Group align="center" gap="sm">
              <IconTarget size={20} color={getSuccessColor(result.success)} />
              <Title order={5}>Test Results</Title>
            </Group>
            <Text size="sm" c="dimmed">
              Routing evaluation for model: {request.model}
            </Text>
          </div>
          <Group>
            <Badge
              color={getSuccessColor(result.success)}
              variant="light"
              leftSection={result.success ? <IconCheck size={12} /> : <IconX size={12} />}
            >
              {result.success ? 'Success' : 'Failed'}
            </Badge>
            <Badge variant="light" leftSection={<IconClock size={12} />}>
              {result.evaluationTime.toFixed(2)}ms
            </Badge>
          </Group>
        </Group>

        {/* Error Messages */}
        {result.errors && result.errors.length > 0 && (
          <Alert icon={<IconAlertTriangle size="1rem" />} title="Errors" color="red">
            <Stack gap="xs">
              {result.errors.map((error) => (
                <Text key={`error-${error.slice(0, 50)}`} size="sm">• {error}</Text>
              ))}
            </Stack>
          </Alert>
        )}

        {/* Quick Summary */}
        <Group grow>
          <Card withBorder p="sm">
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Matched Rules
            </Text>
            <Text fw={700} size="lg">
              {result.matchedRules.filter(r => r.matchedConditions.every(c => c.matched)).length}
            </Text>
          </Card>
          <Card withBorder p="sm">
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Applied Rules
            </Text>
            <Text fw={700} size="lg">
              {result.matchedRules.filter(r => r.applied).length}
            </Text>
          </Card>
          <Card withBorder p="sm">
            <Text size="xs" c="dimmed" fw={700} style={{ textTransform: 'uppercase' }}>
              Selected Provider
            </Text>
            <Text fw={700} size="lg">
              {result.selectedProvider?.name ?? 'None'}
            </Text>
          </Card>
        </Group>

        {/* Matched Rules Details */}
        {result.matchedRules.length > 0 && (
          <Accordion variant="separated">
            <Accordion.Item value="matched-rules">
              <Accordion.Control>
                <Group>
                  <IconListCheck size={16} />
                  <Text fw={500}>Rule Evaluation Details</Text>
                  <Badge size="sm" variant="light">
                    {result.matchedRules.length} rules evaluated
                  </Badge>
                </Group>
              </Accordion.Control>
              <Accordion.Panel>
                <Stack gap="md">
                  {result.matchedRules.map((matchedRule, ruleIndex) => {
                    const allMatched = matchedRule.matchedConditions.every(c => c.matched);
                    
                    return (
                      <Card key={matchedRule.rule.id ?? `rule-${ruleIndex}`} withBorder p="md" bg={allMatched ? 'green.0' : 'gray.0'}>
                        <Stack gap="sm">
                          {/* Rule Header */}
                          <Group justify="space-between" align="center">
                            <div>
                              <Group align="center" gap="sm">
                                <Text fw={600}>{matchedRule.rule.name}</Text>
                                {getRuleStatusBadge(matchedRule)}
                                <Badge size="sm" variant="outline">
                                  Priority: {matchedRule.priority}
                                </Badge>
                              </Group>
                              {matchedRule.rule.description && (
                                <Text size="xs" c="dimmed" mt={2}>
                                  {matchedRule.rule.description}
                                </Text>
                              )}
                            </div>
                          </Group>

                          {/* Conditions Evaluation */}
                          <div>
                            <Text size="sm" fw={500} mb="xs">Conditions Evaluation:</Text>
                            <Table>
                              <Table.Thead>
                                <Table.Tr>
                                  <Table.Th style={{ width: '60px' }}>Result</Table.Th>
                                  <Table.Th>Field</Table.Th>
                                  <Table.Th>Operator</Table.Th>
                                  <Table.Th>Expected</Table.Th>
                                  <Table.Th>Actual</Table.Th>
                                  <Table.Th>Reason</Table.Th>
                                </Table.Tr>
                              </Table.Thead>
                              <Table.Tbody>
                                {matchedRule.matchedConditions.map((condition) => (
                                  <Table.Tr key={`condition-${condition.condition.type}-${condition.condition.field ?? 'no-field'}-${condition.condition.operator}`}>
                                    <Table.Td>
                                      <Tooltip label={condition.matched ? 'Matched' : 'Not matched'}>
                                        {getConditionIcon(condition.matched)}
                                      </Tooltip>
                                    </Table.Td>
                                    <Table.Td>
                                      <Badge size="xs" variant="light">
                                        {condition.condition.type}
                                        {condition.condition.field && `.${condition.condition.field}`}
                                      </Badge>
                                    </Table.Td>
                                    <Table.Td>
                                      <Text size="sm">{condition.condition.operator}</Text>
                                    </Table.Td>
                                    <Table.Td>
                                      <Text size="sm" c="dimmed">
                                        {formatConditionValue(condition.condition, condition.condition.value)}
                                      </Text>
                                    </Table.Td>
                                    <Table.Td>
                                      <Text size="sm" fw={condition.matched ? 500 : 400}>
                                        {formatConditionValue(condition.condition, condition.actualValue)}
                                      </Text>
                                    </Table.Td>
                                    <Table.Td>
                                      <Text size="xs" c={condition.matched ? 'green' : 'red'}>
                                        {condition.reason}
                                      </Text>
                                    </Table.Td>
                                  </Table.Tr>
                                ))}
                              </Table.Tbody>
                            </Table>
                          </div>

                          {/* Actions */}
                          {matchedRule.rule.actions.length > 0 && (
                            <div>
                              <Text size="sm" fw={500} mb="xs">Actions:</Text>
                              <Group gap="xs">
                                {matchedRule.rule.actions.map((action) => (
                                  <Badge
                                    key={`action-${action.type}-${action.target ?? 'no-target'}`}
                                    variant={matchedRule.applied ? 'filled' : 'light'}
                                    color={matchedRule.applied ? 'green' : 'gray'}
                                  >
                                    {action.type}
                                    {action.target && ` → ${action.target}`}
                                  </Badge>
                                ))}
                              </Group>
                            </div>
                          )}
                        </Stack>
                      </Card>
                    );
                  })}
                </Stack>
              </Accordion.Panel>
            </Accordion.Item>
          </Accordion>
        )}

        {/* No Rules Matched */}
        {result.matchedRules.length === 0 && (
          <Alert icon={<IconAlertTriangle size="1rem" />} title="No Rules Matched" color="orange">
            <Text size="sm">
              No routing rules matched your test request. This means the default routing strategy will be used.
            </Text>
          </Alert>
        )}
      </Stack>
    </Card>
  );
}