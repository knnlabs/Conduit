'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  Timeline,
  Badge,
  ThemeIcon,
  Table,
  Progress,
  Tooltip,
} from '@mantine/core';
import {
  IconClock,
  IconCheck,
  IconX,
  IconArrowRight,
  IconBug,
  IconBolt,
} from '@tabler/icons-react';
import { EvaluationStep, MatchedRule } from '../../../types/routing';

interface RuleEvaluationTraceProps {
  evaluationSteps: EvaluationStep[];
  matchedRules: MatchedRule[];
  evaluationTime: number;
}

export function RuleEvaluationTrace({
  evaluationSteps,
  matchedRules,
  evaluationTime,
}: RuleEvaluationTraceProps) {
  const getStepIcon = (step: EvaluationStep) => {
    if (step.success) {
      return <IconCheck size={14} />;
    } else {
      return <IconX size={14} />;
    }
  };

  const getStepColor = (step: EvaluationStep) => {
    if (step.success) {
      return 'green';
    } else {
      return 'red';
    }
  };

  const formatDuration = (duration: number) => {
    if (duration < 1) {
      return '< 1ms';
    }
    return `${duration.toFixed(2)}ms`;
  };

  const getTotalDuration = () => {
    return evaluationSteps.reduce((sum, step) => sum + step.duration, 0);
  };

  const getPerformanceColor = (duration: number) => {
    if (duration < 1) return 'green';
    if (duration < 5) return 'yellow';
    return 'orange';
  };

  const groupedSteps = evaluationSteps.reduce((acc, step) => {
    const category = step.action.split('.')[0] || 'general';
    if (!acc[category]) {
      acc[category] = [];
    }
    acc[category].push(step);
    return acc;
  }, {} as Record<string, EvaluationStep[]>);

  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Stack gap="md">
        {/* Header */}
        <Group justify="space-between" align="flex-start">
          <div>
            <Group align="center" gap="sm">
              <IconBug size={20} color="blue" />
              <Title order={5}>Rule Evaluation Timeline</Title>
            </Group>
            <Text size="sm" c="dimmed">
              Step-by-step breakdown of the routing evaluation process
            </Text>
          </div>
          <Group>
            <Badge variant="light" leftSection={<IconClock size={12} />}>
              Total: {formatDuration(getTotalDuration())}
            </Badge>
            <Badge variant="light" leftSection={<IconBolt size={12} />}>
              {evaluationSteps.length} steps
            </Badge>
          </Group>
        </Group>

        {/* Performance Overview */}
        <Card withBorder p="sm" bg="gray.0">
          <Group justify="space-between" align="center" mb="xs">
            <Text size="sm" fw={500}>Performance Breakdown</Text>
            <Text size="xs" c="dimmed">
              {formatDuration(evaluationTime)} total evaluation time
            </Text>
          </Group>
          
          <Stack gap="xs">
            {Object.entries(groupedSteps).map(([category, steps]) => {
              const categoryDuration = steps.reduce((sum, step) => sum + step.duration, 0);
              const percentage = (categoryDuration / getTotalDuration()) * 100;
              
              return (
                <div key={category}>
                  <Group justify="space-between" mb={2}>
                    <Text size="xs" fw={500} style={{ textTransform: 'capitalize' }}>
                      {category.replace(/([A-Z])/g, ' $1').trim()}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {formatDuration(categoryDuration)} ({percentage.toFixed(1)}%)
                    </Text>
                  </Group>
                  <Progress
                    value={percentage}
                    size="xs"
                    color={getPerformanceColor(categoryDuration)}
                  />
                </div>
              );
            })}
          </Stack>
        </Card>

        {/* Detailed Timeline */}
        {evaluationSteps.length > 0 && (
          <div>
            <Text fw={500} size="sm" mb="md">Detailed Execution Steps</Text>
            <Timeline active={evaluationSteps.length} bulletSize={24} lineWidth={2}>
              {evaluationSteps.map((step, index) => (
                <Timeline.Item
                  key={index}
                  bullet={
                    <ThemeIcon
                      size={20}
                      variant="light"
                      color={getStepColor(step)}
                    >
                      {getStepIcon(step)}
                    </ThemeIcon>
                  }
                  title={
                    <Group justify="space-between" align="center">
                      <div>
                        <Text fw={500} size="sm">
                          Step {step.stepNumber}: {step.action}
                        </Text>
                        {step.ruleName && (
                          <Badge size="xs" variant="light" mt={2}>
                            Rule: {step.ruleName}
                          </Badge>
                        )}
                      </div>
                      <Group gap="xs">
                        <Badge
                          size="xs"
                          color={getPerformanceColor(step.duration)}
                          variant="light"
                        >
                          {formatDuration(step.duration)}
                        </Badge>
                        <Badge
                          size="xs"
                          color={getStepColor(step)}
                          variant="light"
                        >
                          {step.success ? 'Success' : 'Failed'}
                        </Badge>
                      </Group>
                    </Group>
                  }
                >
                  <Text size="sm" c="dimmed" mt="xs">
                    {step.details}
                  </Text>
                  {step.timestamp && (
                    <Text size="xs" c="dimmed" mt="xs">
                      Timestamp: {new Date(step.timestamp).toLocaleTimeString()}
                    </Text>
                  )}
                </Timeline.Item>
              ))}
            </Timeline>
          </div>
        )}

        {/* Summary Table */}
        {evaluationSteps.length > 0 && (
          <div>
            <Text fw={500} size="sm" mb="md">Execution Summary</Text>
            <Table>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Category</Table.Th>
                  <Table.Th>Steps</Table.Th>
                  <Table.Th>Success Rate</Table.Th>
                  <Table.Th>Total Duration</Table.Th>
                  <Table.Th>Avg Duration</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {Object.entries(groupedSteps).map(([category, steps]) => {
                  const successCount = steps.filter(s => s.success).length;
                  const successRate = (successCount / steps.length) * 100;
                  const totalDuration = steps.reduce((sum, step) => sum + step.duration, 0);
                  const avgDuration = totalDuration / steps.length;
                  
                  return (
                    <Table.Tr key={category}>
                      <Table.Td>
                        <Text fw={500} size="sm" style={{ textTransform: 'capitalize' }}>
                          {category.replace(/([A-Z])/g, ' $1').trim()}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge size="sm" variant="light">{steps.length}</Badge>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Progress
                            value={successRate}
                            size="sm"
                            w={60}
                            color={successRate === 100 ? 'green' : successRate > 80 ? 'yellow' : 'red'}
                          />
                          <Text size="sm">{successRate.toFixed(0)}%</Text>
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" color={getPerformanceColor(totalDuration)}>
                          {formatDuration(totalDuration)}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" c="dimmed">
                          {formatDuration(avgDuration)}
                        </Text>
                      </Table.Td>
                    </Table.Tr>
                  );
                })}
              </Table.Tbody>
            </Table>
          </div>
        )}

        {/* Empty State */}
        {evaluationSteps.length === 0 && (
          <Text c="dimmed" ta="center" py="xl">
            No evaluation steps recorded for this test
          </Text>
        )}
      </Stack>
    </Card>
  );
}