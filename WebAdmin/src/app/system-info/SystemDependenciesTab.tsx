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
import { IconPackage } from '@tabler/icons-react';
import { SystemInfoDto } from '@knn_labs/conduit-admin-client';

interface SystemDependenciesTabProps {
  systemInfo: SystemInfoDto | null;
}

export function SystemDependenciesTab({ systemInfo }: SystemDependenciesTabProps) {
  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Title order={4} mb="md">System Dependencies</Title>
      <Alert
        icon={<IconPackage size={16} />}
        title="Information"
        color="blue"
        mb="md"
      >
        Package dependency information is not available via the system API. Check package.json files directly for detailed dependency information.
      </Alert>
      <ScrollArea>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Component</Table.Th>
              <Table.Th>Version</Table.Th>
              <Table.Th>Status</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            <Table.Tr>
              <Table.Td>
                <Code>Conduit Core</Code>
              </Table.Td>
              <Table.Td>{systemInfo?.version ?? 'Unknown'}</Table.Td>
              <Table.Td>
                <Badge variant="light" color="green">
                  Current
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>.NET Runtime</Code>
              </Table.Td>
              <Table.Td>{systemInfo?.runtime?.dotnetVersion ?? 'Unknown'}</Table.Td>
              <Table.Td>
                <Badge variant="light" color="green">
                  Runtime
                </Badge>
              </Table.Td>
            </Table.Tr>
            <Table.Tr>
              <Table.Td>
                <Code>Database Provider</Code>
              </Table.Td>
              <Table.Td>{systemInfo?.database?.provider ?? 'Unknown'}</Table.Td>
              <Table.Td>
                <Badge 
                  variant="light" 
                  color={systemInfo?.database?.isConnected ? 'green' : 'red'}
                >
                  {systemInfo?.database?.isConnected ? 'Connected' : 'Disconnected'}
                </Badge>
              </Table.Td>
            </Table.Tr>
          </Table.Tbody>
        </Table>
      </ScrollArea>
    </Card>
  );
}