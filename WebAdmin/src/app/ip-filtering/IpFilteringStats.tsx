'use client';

import {
  IconShield,
  IconShieldCheck,
  IconShieldX,
  IconClock,
} from '@tabler/icons-react';
import { type IpStats } from '@/hooks/useSecurityApi';
import {
  StatCardGrid,
  type StatCardItem,
} from '@/components/common/StatCardGrid';

interface IpFilteringStatsProps {
  stats: IpStats | null;
  isLoading?: boolean;
}

export function IpFilteringStats({ stats, isLoading = false }: IpFilteringStatsProps) {
  const statCards: StatCardItem[] = [
    {
      title: 'Total Rules',
      value: stats?.totalRules ?? 0,
      description: 'Active IP filtering rules',
      icon: IconShield,
      color: 'blue',
    },
    {
      title: 'Allow Rules',
      value: stats?.allowRules ?? 0,
      description: 'Whitelisted IPs',
      icon: IconShieldCheck,
      color: 'green',
    },
    {
      title: 'Block Rules',
      value: stats?.blockRules ?? 0,
      description: 'Blacklisted IPs',
      icon: IconShieldX,
      color: 'red',
    },
    // Note: no "Blocked Today" card — blocked-request counts are not tracked
    // server-side, and a hardcoded 0 would read as "no attacks in 24h"
    {
      title: 'Active Rules',
      value: stats?.activeRules ?? 0,
      description: 'Currently enabled rules',
      icon: IconClock,
      color: 'orange',
    },
  ];

  return (
    <StatCardGrid
      items={statCards}
      cols={{ base: 1, sm: 2, lg: 4 }}
      spacing="md"
      loading={isLoading}
    />
  );
}
