'use client';

import {
  Card,
  Title,
  Table,
  Badge,
  Code,
  Alert,
  ScrollArea,
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
                <Code>Operating System</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.operatingSystem?.description ?? 'Unknown'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" size="sm" color="blue">
                  System
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Architecture</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.operatingSystem?.architecture ?? 'Unknown'}</Code>
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
                <Code>{systemInfo?.version?.buildDate ? new Date(systemInfo.version.buildDate).toLocaleDateString() : 'Unknown'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" size="sm" color="blue">
                  System
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Database Version</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.database?.version ?? 'Unknown'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" size="sm" color="blue">
                  Database
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Database Tables</Code>
              </Table.Td>
              <Table.Td>
                <Code>{systemInfo?.database?.tableCount ?? 'Unknown'}</Code>
              </Table.Td>
              <Table.Td>
                <Badge variant="light" size="sm" color="blue">
                  Database
                </Badge>
              </Table.Td>
            </Table.Tr>
          </Table.Tbody>
        </Table>
      </ScrollArea>
    </Card>
  );
}