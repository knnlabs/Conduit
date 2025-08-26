import { Card, Group, ThemeIcon, Text, List } from '@mantine/core';
import type { CheckResult } from './types';

interface CheckItemProps {
  check: CheckResult;
  getStatusBadge: (status: CheckResult['status']) => React.ReactNode;
}

export function CheckItem({ check, getStatusBadge }: CheckItemProps) {
  return (
    <Card withBorder padding="sm">
      <Group justify="space-between" align="flex-start">
        <Group align="flex-start">
          <ThemeIcon size="sm" variant="subtle" color="gray">
            <check.icon size={14} />
          </ThemeIcon>
          <div style={{ flex: 1 }}>
            <Group gap="xs" mb="xs">
              <Text size="sm" fw={500}>{check.title}</Text>
              {getStatusBadge(check.status)}
            </Group>
            <Text size="xs" c="dimmed" mb={check.details ? "xs" : 0}>
              {check.message}
            </Text>
            {check.details && (
              <List size="xs" spacing="xs">
                {check.details.map((detail, idx) => (
                  <List.Item key={idx}>{detail}</List.Item>
                ))}
              </List>
            )}
          </div>
        </Group>
      </Group>
    </Card>
  );
}