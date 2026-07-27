import {
  Card,
  Group,
  SimpleGrid,
  Stack,
  Text,
  ThemeIcon,
  type SimpleGridProps,
} from '@mantine/core';
import { IconLoader2, type TablerIcon } from '@tabler/icons-react';

export interface StatCardItem {
  title: string;
  value: number | string;
  description?: string;
  icon: TablerIcon;
  color: string;
}

interface StatCardGridProps {
  items: StatCardItem[];
  cols?: SimpleGridProps['cols'];
  spacing?: SimpleGridProps['spacing'];
  loading?: boolean;
  loadingCount?: number;
}

function displayValue(value: number | string): string {
  return typeof value === 'number' ? value.toLocaleString() : value;
}

export function StatCardGrid({
  items,
  cols = { base: 1, sm: 2, md: 4 },
  spacing = 'lg',
  loading = false,
  loadingCount = 4,
}: StatCardGridProps) {
  const cards: StatCardItem[] = loading
    ? Array.from({ length: loadingCount }, () => ({
        title: 'Loading...',
        value: '--',
        description: 'Loading statistics...',
        icon: items[0]?.icon ?? IconLoader2,
        color: 'gray',
      }))
    : items;

  return (
    <SimpleGrid cols={cols} spacing={spacing}>
      {cards.map((stat, index) => {
        const detailed = stat.description !== undefined;
        return (
          <Card
            key={loading ? index : stat.title}
            p={detailed ? 'lg' : 'md'}
            radius={detailed ? 'md' : undefined}
            withBorder
          >
            <Stack gap={detailed ? 'md' : 0}>
              <Group justify="space-between" align="flex-start">
                <Stack gap={4} style={{ flex: 1 }}>
                  <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                    {stat.title}
                  </Text>
                  <Text fw={700} size="xl" lh={detailed ? 1 : undefined}>
                    {displayValue(stat.value)}
                  </Text>
                </Stack>
                <ThemeIcon
                  size={detailed ? 40 : 'lg'}
                  radius={detailed ? 'md' : undefined}
                  variant="light"
                  color={stat.color}
                >
                  <stat.icon size={20} />
                </ThemeIcon>
              </Group>
              {stat.description && (
                <Text size="xs" c="dimmed" lh={1.2}>
                  {stat.description}
                </Text>
              )}
            </Stack>
          </Card>
        );
      })}
    </SimpleGrid>
  );
}
