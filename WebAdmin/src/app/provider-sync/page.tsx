'use client';

import { useState, useCallback } from 'react';
import { Container, Title, Text, Button, Group, Stack, Tabs } from '@mantine/core';
import { IconRefresh, IconGitCompare, IconHistory } from '@tabler/icons-react';
import { DriftTable } from './components/DriftTable';
import { SyncRunsPanel } from './components/SyncRunsPanel';
import { useRunSync } from './hooks/useProviderSyncApi';

export default function ProviderSyncPage() {
  const [activeTab, setActiveTab] = useState<string | null>('drift');
  const [visitedTabs, setVisitedTabs] = useState<Set<string>>(new Set(['drift']));
  const runSync = useRunSync();

  const handleTabChange = useCallback((value: string | null) => {
    setActiveTab(value);
    if (value) {
      setVisitedTabs((prev) => {
        if (prev.has(value)) return prev;
        const next = new Set(prev);
        next.add(value);
        return next;
      });
    }
  }, []);

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between">
          <div>
            <Title order={2}>Provider Sync</Title>
            <Text c="dimmed" size="sm">
              Review pricing, context-window, and capability drift detected against OpenRouter&apos;s
              published catalog, then apply or dismiss each change.
            </Text>
          </div>
          <Button
            leftSection={<IconRefresh size={16} />}
            onClick={() => runSync.mutate()}
            loading={runSync.isPending}
          >
            Run sync now
          </Button>
        </Group>

        <Tabs value={activeTab} onChange={handleTabChange}>
          <Tabs.List>
            <Tabs.Tab value="drift" leftSection={<IconGitCompare size={16} />}>
              Drift Review
            </Tabs.Tab>
            <Tabs.Tab value="runs" leftSection={<IconHistory size={16} />}>
              Sync Runs
            </Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="drift" pt="md">
            <DriftTable />
          </Tabs.Panel>

          <Tabs.Panel value="runs" pt="md">
            {visitedTabs.has('runs') && <SyncRunsPanel />}
          </Tabs.Panel>
        </Tabs>
      </Stack>
    </Container>
  );
}
