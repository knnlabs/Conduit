import { Table, Badge, Group, Text, Button, ActionIcon, Tooltip } from '@mantine/core';
import { IconRefresh, IconKey } from '@tabler/icons-react';
import { modals } from '@mantine/modals';
import type { components } from '@knn_labs/conduit-admin-client';

type ProviderErrorSummaryDto = components['schemas']['ConduitLLM.Admin.DTOs.ProviderErrorSummaryDto'];

interface ProviderErrorTableProps {
  summaries: ProviderErrorSummaryDto[];
  onClearErrors: (keyId: number, reenableKey: boolean) => Promise<void>;
}

export function ProviderErrorTable({ summaries, onClearErrors }: ProviderErrorTableProps) {
  const handleViewDisabledKeys = (summary: ProviderErrorSummaryDto) => {
    const keyIds = summary.disabledKeyIds ?? [];
    
    modals.open({
      title: `Disabled Keys for ${summary.providerName}`,
      children: (
        <div>
          {keyIds.length === 0 ? (
            <Text c="dimmed">No disabled keys</Text>
          ) : (
            <>
              <Text size="sm" mb="md">
                The following key IDs have been disabled due to errors:
              </Text>
              <Group gap="xs">
                {keyIds.map((keyId) => (
                  <Badge key={keyId} color="red" variant="light">
                    Key #{keyId}
                  </Badge>
                ))}
              </Group>
              <Text size="xs" c="dimmed" mt="md">
                Clear errors and re-enable keys from the individual key management page.
              </Text>
            </>
          )}
        </div>
      ),
    });
  };

  const formatLastError = (lastError: string | undefined | null) => {
    if (!lastError) return 'N/A';
    const date = new Date(lastError);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    
    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes} min ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours} hour${hours > 1 ? 's' : ''} ago`;
    const days = Math.floor(hours / 24);
    return `${days} day${days > 1 ? 's' : ''} ago`;
  };

  if (summaries.length === 0) {
    return (
      <Text c="dimmed" ta="center" py="lg">
        No provider errors to display
      </Text>
    );
  }

  return (
    <Table>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>Provider</Table.Th>
          <Table.Th>Total Errors</Table.Th>
          <Table.Th>Fatal</Table.Th>
          <Table.Th>Warnings</Table.Th>
          <Table.Th>Disabled Keys</Table.Th>
          <Table.Th>Last Error</Table.Th>
          <Table.Th>Actions</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {summaries.map((summary) => (
          <Table.Tr key={summary.providerId}>
            <Table.Td>
              <Text fw={500}>{summary.providerName}</Text>
            </Table.Td>
            <Table.Td>
              <Badge
                color={(summary.totalErrors ?? 0) > 0 ? 'gray' : 'green'}
                variant="light"
              >
                {summary.totalErrors ?? 0}
              </Badge>
            </Table.Td>
            <Table.Td>
              {(summary.fatalErrors ?? 0) > 0 && (
                <Badge color="red" variant="light">
                  {summary.fatalErrors}
                </Badge>
              )}
              {(summary.fatalErrors ?? 0) === 0 && (
                <Text size="sm" c="dimmed">-</Text>
              )}
            </Table.Td>
            <Table.Td>
              {(summary.warnings ?? 0) > 0 && (
                <Badge color="yellow" variant="light">
                  {summary.warnings}
                </Badge>
              )}
              {(summary.warnings ?? 0) === 0 && (
                <Text size="sm" c="dimmed">-</Text>
              )}
            </Table.Td>
            <Table.Td>
              {(summary.disabledKeyIds?.length ?? 0) > 0 ? (
                <Button
                  size="xs"
                  variant="subtle"
                  color="orange"
                  leftSection={<IconKey size={14} />}
                  onClick={() => handleViewDisabledKeys(summary)}
                >
                  {summary.disabledKeyIds?.length} disabled
                </Button>
              ) : (
                <Text size="sm" c="dimmed">None</Text>
              )}
            </Table.Td>
            <Table.Td>
              <Text size="sm" c="dimmed">
                {formatLastError(summary.lastError)}
              </Text>
            </Table.Td>
            <Table.Td>
              <Group gap="xs">
                {(summary.disabledKeyIds?.length ?? 0) > 0 && (
                  <Tooltip label="Clear all errors and re-enable keys">
                    <ActionIcon
                      variant="light"
                      color="teal"
                      size="sm"
                      onClick={() => {
                        modals.openConfirmModal({
                          title: 'Clear Errors and Re-enable Keys',
                          children: (
                            <Text size="sm">
                              This will clear all error history for {summary.providerName} and re-enable all disabled keys.
                              Are you sure you want to proceed?
                            </Text>
                          ),
                          labels: { confirm: 'Clear & Re-enable', cancel: 'Cancel' },
                          confirmProps: { color: 'teal' },
                          onConfirm: () => {
                            void (async () => {
                              for (const keyId of summary.disabledKeyIds ?? []) {
                                await onClearErrors(keyId, true);
                              }
                            })();
                          },
                        });
                      }}
                    >
                      <IconRefresh size={16} />
                    </ActionIcon>
                  </Tooltip>
                )}
              </Group>
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}