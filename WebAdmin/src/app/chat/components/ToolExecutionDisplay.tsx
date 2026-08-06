import { Stack, Group, Paper, Text, Badge, Code } from '@mantine/core';
import { IconTool, IconLoader, IconCircleCheck, IconCircleX } from '@tabler/icons-react';

interface ToolExecution {
  tool_call_id?: string;
  function_name: string;
  status: string;
  result?: unknown;
  error_message?: string;
  cost?: number;
}

function getStatusColor(isFailed: boolean, isCompleted: boolean): string {
  if (isFailed) return 'red';
  if (isCompleted) return 'green';
  return 'blue';
}

function getStatusBgColor(isFailed: boolean, isCompleted: boolean): string {
  if (isFailed) return 'var(--mantine-color-red-light)';
  if (isCompleted) return 'var(--mantine-color-green-light)';
  return 'var(--mantine-color-blue-light)';
}

interface ToolExecutionDisplayProps {
  executions: ToolExecution[];
}

export function ToolExecutionDisplay({ executions }: ToolExecutionDisplayProps) {
  if (executions.length === 0) return null;

  return (
    <Stack gap="xs">
      <Group gap="xs">
        <IconTool size={14} />
        <Text size="xs" fw={600}>Tool Execution:</Text>
      </Group>
      {executions.map((execution, idx) => {
        const isStarted = execution.status === 'started';
        const isCompleted = execution.status === 'completed';
        const isFailed = execution.status === 'failed';

        return (
          <Paper
            key={execution.tool_call_id ?? `${execution.function_name}-${idx}`}
            p="xs"
            radius="sm"
            withBorder
            style={{
              backgroundColor: getStatusBgColor(isFailed, isCompleted)
            }}
          >
            <Group justify="space-between" wrap="nowrap">
              <Group gap="xs">
                {isStarted && <IconLoader size={14} className="rotating-icon" />}
                {isCompleted && <IconCircleCheck size={14} color="var(--mantine-color-green-6)" />}
                {isFailed && <IconCircleX size={14} color="var(--mantine-color-red-6)" />}
                <Text size="xs" fw={500}>{execution.function_name}</Text>
              </Group>
              <Badge
                size="xs"
                color={getStatusColor(isFailed, isCompleted)}
                variant="light"
              >
                {execution.status}
              </Badge>
            </Group>

            {execution.error_message && (
              <Text size="xs" c="red" mt={4}>
                Error: {execution.error_message}
              </Text>
            )}

            {execution.result !== undefined && (
              <Code block mt={4} style={{ fontSize: '0.7rem', maxHeight: '100px', overflow: 'auto' }}>
                {typeof execution.result === 'string'
                  ? execution.result
                  : JSON.stringify(execution.result, null, 2)}
              </Code>
            )}

            {execution.cost !== undefined && execution.cost > 0 && (
              <Text size="xs" c="dimmed" mt={4}>
                Cost: ${execution.cost.toFixed(4)}
              </Text>
            )}
          </Paper>
        );
      })}
    </Stack>
  );
}
