'use client';

import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  Button,
  ScrollArea,
  Badge,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import {
  IconHistory,
  IconTrash,
  IconClock,
  IconFlask,
  IconDownload,
  IconRefresh,
} from '@tabler/icons-react';
import { TestHistory as TestHistoryType, TestCase } from '../../../types/routing';

interface TestHistoryProps {
  history: TestHistoryType;
  onLoadTestCase: (testCase: TestCase) => void;
  onClearHistory: () => void;
  selectedCase?: TestCase | null;
}

export function TestHistory({
  history,
  onLoadTestCase,
  onClearHistory,
  selectedCase,
}: TestHistoryProps) {
  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMinutes = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMinutes < 1) {
      return 'Just now';
    } else if (diffMinutes < 60) {
      return `${diffMinutes}m ago`;
    } else if (diffHours < 24) {
      return `${diffHours}h ago`;
    } else if (diffDays < 7) {
      return `${diffDays}d ago`;
    } else {
      return date.toLocaleDateString();
    }
  };

  const getModelBadgeColor = (model: string) => {
    if (model.includes('gpt-4')) return 'blue';
    if (model.includes('claude')) return 'purple';
    if (model.includes('gemini')) return 'green';
    if (model.includes('llama')) return 'orange';
    return 'gray';
  };

  const generateTestSummary = (testCase: TestCase) => {
    const parts = [testCase.request.model];
    
    if (testCase.request.region) {
      parts.push(testCase.request.region);
    }
    
    if (testCase.request.costThreshold) {
      parts.push(`$${testCase.request.costThreshold}`);
    }
    
    const customFieldCount = Object.keys(testCase.request.customFields).length;
    if (customFieldCount > 0) {
      parts.push(`+${customFieldCount} fields`);
    }
    
    return parts.join(' • ');
  };

  return (
    <Card shadow="sm" p="md" radius="md" withBorder h="fit-content">
      <Stack gap="md">
        {/* Header */}
        <Group justify="space-between" align="center">
          <Group align="center" gap="sm">
            <IconHistory size={20} color="gray" />
            <Title order={5}>Test History</Title>
          </Group>
          <Group gap="xs">
            <Badge size="sm" variant="light">
              {history.cases.length} tests
            </Badge>
            {history.cases.length > 0 && (
              <Tooltip label="Clear all history">
                <ActionIcon
                  variant="subtle"
                  color="red"
                  size="sm"
                  onClick={onClearHistory}
                >
                  <IconTrash size={14} />
                </ActionIcon>
              </Tooltip>
            )}
          </Group>
        </Group>

        {/* Last Run Info */}
        {history.lastRun && (
          <Text size="xs" c="dimmed">
            Last run: {formatTimestamp(history.lastRun)}
          </Text>
        )}

        {/* Test Cases List */}
        {history.cases.length > 0 ? (
          <ScrollArea h={400}>
            <Stack gap="sm">
              {history.cases
                .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
                .map((testCase) => (
                  <Card
                    key={testCase.id}
                    withBorder
                    p="sm"
                    style={{
                      cursor: 'pointer',
                      backgroundColor: selectedCase?.id === testCase.id ? 'var(--mantine-color-blue-0)' : undefined,
                    }}
                    onClick={() => onLoadTestCase(testCase)}
                  >
                    <Stack gap="xs">
                      {/* Test Case Header */}
                      <Group justify="space-between" align="center">
                        <div style={{ flex: 1 }}>
                          <Text fw={500} size="sm" lineClamp={1}>
                            {testCase.name}
                          </Text>
                          <Text size="xs" c="dimmed" lineClamp={1}>
                            {generateTestSummary(testCase)}
                          </Text>
                        </div>
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            onLoadTestCase(testCase);
                          }}
                        >
                          <IconRefresh size={12} />
                        </ActionIcon>
                      </Group>

                      {/* Model and Timestamp */}
                      <Group justify="space-between" align="center">
                        <Badge
                          size="xs"
                          variant="light"
                          color={getModelBadgeColor(testCase.request.model)}
                        >
                          {testCase.request.model}
                        </Badge>
                        <Group gap={4} align="center">
                          <IconClock size={10} color="gray" />
                          <Text size="xs" c="dimmed">
                            {formatTimestamp(testCase.timestamp)}
                          </Text>
                        </Group>
                      </Group>

                      {/* Additional Info */}
                      {((testCase.request.region ?? false) || 
                        testCase.request.costThreshold !== undefined || 
                        (testCase.request.virtualKeyId ?? false) ||
                        Object.keys(testCase.request.customFields).length > 0) && (
                        <Group gap="xs">
                          {testCase.request.region && (
                            <Badge size="xs" variant="outline">
                              {testCase.request.region}
                            </Badge>
                          )}
                          {testCase.request.costThreshold !== undefined && (
                            <Badge size="xs" variant="outline" color="green">
                              ${testCase.request.costThreshold}
                            </Badge>
                          )}
                          {testCase.request.virtualKeyId && (
                            <Badge size="xs" variant="outline" color="purple">
                              VK
                            </Badge>
                          )}
                          {Object.keys(testCase.request.customFields).length > 0 && (
                            <Badge size="xs" variant="outline" color="orange">
                              +{Object.keys(testCase.request.customFields).length}
                            </Badge>
                          )}
                        </Group>
                      )}

                      {/* Description */}
                      {testCase.description && (
                        <Text size="xs" c="dimmed" lineClamp={2}>
                          {testCase.description}
                        </Text>
                      )}
                    </Stack>
                  </Card>
                ))}
            </Stack>
          </ScrollArea>
        ) : (
          <Card withBorder p="xl" bg="gray.0">
            <Stack align="center" gap="md">
              <IconFlask size={32} stroke={1.5} color="gray" />
              <div style={{ textAlign: 'center' }}>
                <Text size="sm" fw={500}>No Test History</Text>
                <Text size="xs" c="dimmed" mt={4}>
                  Run some tests to see them appear here
                </Text>
              </div>
            </Stack>
          </Card>
        )}

        {/* Quick Actions */}
        {history.cases.length > 0 && (
          <Group grow>
            <Button
              variant="light"
              size="xs"
              leftSection={<IconDownload size={14} />}
              onClick={() => {
                // Export history functionality would go here
                const dataStr = JSON.stringify(history, null, 2);
                const dataBlob = new Blob([dataStr], { type: 'application/json' });
                const url = URL.createObjectURL(dataBlob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `test-history-${new Date().toISOString().split('T')[0]}.json`;
                link.click();
              }}
            >
              Export
            </Button>
          </Group>
        )}
      </Stack>
    </Card>
  );
}