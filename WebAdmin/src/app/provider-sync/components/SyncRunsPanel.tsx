'use client';

import { useQuery } from '@tanstack/react-query';
import {
  Table,
  ScrollArea,
  Badge,
  Text,
  LoadingOverlay,
  Center,
  Card,
  Box,
  Tooltip,
} from '@mantine/core';
import { formatters } from '@/lib/utils/formatters';
import { fetchSyncRuns, RUNS_QUERY_KEY } from '../hooks/useProviderSyncApi';

function runStatusColor(status: string): string {
  switch (status) {
    case 'Completed':
      return 'green';
    case 'Running':
      return 'blue';
    case 'Failed':
      return 'red';
    default:
      return 'gray';
  }
}

export function SyncRunsPanel() {
  const { data, isLoading, error } = useQuery({
    queryKey: [RUNS_QUERY_KEY],
    queryFn: () => fetchSyncRuns(1, 25),
  });

  const runs = data ?? [];

  return (
    <Card withBorder padding={0}>
      <Card.Section p="md">
        <Box pos="relative">
          <LoadingOverlay visible={isLoading} />

          {error && (
            <Center py="xl">
              <Text c="red">Failed to load sync runs</Text>
            </Center>
          )}

          {!error && runs.length === 0 && !isLoading && (
            <Center py="xl">
              <Text c="dimmed">No sync runs yet</Text>
            </Center>
          )}

          {!error && runs.length > 0 && (
            <ScrollArea>
              <Table verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Started</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Trigger</Table.Th>
                    <Table.Th>Models</Table.Th>
                    <Table.Th>Mappings</Table.Th>
                    <Table.Th>New</Table.Th>
                    <Table.Th>Updated</Table.Th>
                    <Table.Th>Auto-resolved</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {runs.map((run) => (
                    <Table.Tr key={run.id}>
                      <Table.Td>
                        <Text size="xs">{formatters.date(run.startedAt)}</Text>
                      </Table.Td>
                      <Table.Td>
                        {run.errorMessage ? (
                          <Tooltip label={run.errorMessage} multiline maw={320}>
                            <Badge color={runStatusColor(run.status)} variant="light" size="sm">
                              {run.status}
                            </Badge>
                          </Tooltip>
                        ) : (
                          <Badge color={runStatusColor(run.status)} variant="light" size="sm">
                            {run.status}
                          </Badge>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{run.triggeredBy}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{run.modelsFetched}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{run.mappingsChecked}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{run.itemsCreated}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{run.itemsUpdated}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">{run.itemsAutoResolved}</Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
          )}
        </Box>
      </Card.Section>
    </Card>
  );
}
