'use client';

import {
  Card,
  Title,
  Table,
  Badge,
  Code,
  Alert,
  ScrollArea,
  Stack,
} from '@mantine/core';
import { IconLock } from '@tabler/icons-react';
import { SystemInfoDto } from '@knn_labs/conduit-admin-client';

interface SystemEnvironmentTabProps {
  systemInfo: SystemInfoDto | null;
}

export function SystemEnvironmentTab({ systemInfo }: SystemEnvironmentTabProps) {
  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Title order={4} mb="md">System Configuration</Title>
      <Alert
        icon={<IconLock size={16} />}
        title="Security Notice"
        color="blue"
        mb="md"
      >
        Environment variables are not exposed via the API for security reasons. Configuration values are shown below where available.
      </Alert>
      <ScrollArea>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Setting</Table.Th>
              <Table.Th>Value</Table.Th>
              <Table.Th>Status</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            <Table.Tr>
              <Table.Td>
                <Code>Environment</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.environment ?? 'Unknown'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" size="sm" color="blue">
                  System
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Build Date</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.buildDate ? new Date(systemInfo.buildDate).toLocaleDateString() : 'Unknown'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" size="sm" color="blue">
                  System
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>IP Filtering</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.features?.ipFiltering ? 'Enabled' : 'Disabled'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge 
                  variant="light" 
                  size="sm" 
                  color={systemInfo?.features?.ipFiltering ? 'green' : 'gray'}
                >
                  Feature
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Provider Health</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.features?.providerHealth ? 'Enabled' : 'Disabled'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge 
                  variant="light" 
                  size="sm" 
                  color={systemInfo?.features?.providerHealth ? 'green' : 'gray'}
                >
                  Feature
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Cost Tracking</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.features?.costTracking ? 'Enabled' : 'Disabled'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge 
                  variant="light" 
                  size="sm" 
                  color={systemInfo?.features?.costTracking ? 'green' : 'gray'}
                >
                  Feature
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Audio Support</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.features?.audioSupport ? 'Enabled' : 'Disabled'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge 
                  variant="light" 
                  size="sm" 
                  color={systemInfo?.features?.audioSupport ? 'green' : 'gray'}
                >
                  Feature
                </Badge>
              </Table.Td>
            </Table.Tr>
            {systemInfo?.database?.pendingMigrations && Array.isArray(systemInfo.database.pendingMigrations) && systemInfo.database.pendingMigrations.length > 0 && (
              <Table.Tr>
                <Table.Td>
                  <Code>Pending Migrations</Code>
                </Table.Td>
                <Table.Td>
                  <Stack gap="xs">
                    {systemInfo.database.pendingMigrations.map((migration) => (
                      <Code key={migration}>{String(migration)}</Code>
                    ))}
                  </Stack>
                </Table.Td>
                <Table.Td>
                  <Badge variant="light" size="sm" color="orange">
                    Database
                  </Badge>
                </Table.Td>
              </Table.Tr>
            )}
          </Table.Tbody>
        </Table>
      </ScrollArea>
    </Card>
  );
}