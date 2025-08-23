'use client';

import {
  Card,
  Title,
  Table,
  Badge,
  Text,
  Code,
  ScrollArea,
} from '@mantine/core';
import {
  IconCircleCheck,
  IconAlertTriangle,
} from '@tabler/icons-react';
import { SystemInfoDto } from '@knn_labs/conduit-admin-client';
import { generateServiceInfo, getStatusColor } from './helpers';

interface SystemServicesTabProps {
  systemInfo: SystemInfoDto | null;
}

export function SystemServicesTab({ systemInfo }: SystemServicesTabProps) {
  const services = generateServiceInfo(systemInfo);

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'running':
      case 'healthy':
      case 'latest':
        return <IconCircleCheck size={16} />;
      case 'degraded':
      case 'warning':
      case 'outdated':
        return <IconAlertTriangle size={16} />;
      default:
        return <IconAlertTriangle size={16} />;
    }
  };

  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Title order={4} mb="md">Running Services</Title>
      <ScrollArea>
        <Table>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Service</Table.Th>
              <Table.Th>Version</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Uptime</Table.Th>
              <Table.Th>Port</Table.Th>
              <Table.Th>Resources</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {services.map((service) => (
              <Table.Tr key={service.name}>
                <Table.Td>
                  <Text fw={500}>{service.name}</Text>
                </Table.Td>
                <Table.Td>
                  <Code>{service.version}</Code>
                </Table.Td>
                <Table.Td>
                  <Badge
                    leftSection={getStatusIcon(service.status)}
                    color={getStatusColor(service.status)}
                    variant="light"
                  >
                    {service.status}
                  </Badge>
                </Table.Td>
                <Table.Td>{service.uptime ?? '-'}</Table.Td>
                <Table.Td>{service.port ?? '-'}</Table.Td>
                <Table.Td>
                  <Text size="sm">
                    CPU: {service.cpu ?? '-'}, Mem: {service.memory ?? '-'}
                  </Text>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </ScrollArea>
    </Card>
  );
}