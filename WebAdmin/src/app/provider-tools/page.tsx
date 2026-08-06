'use client';

import { useState } from 'react';
import { Container, Title, Text, Button, Group, Stack } from '@mantine/core';
import { IconPlus, IconRefresh } from '@tabler/icons-react';
import { ProviderToolsTable } from '@/components/provider-tools/ProviderToolsTable';
import { CreateProviderToolModal } from '@/components/provider-tools/CreateProviderToolModal';
import { ImportProviderToolsModal } from '@/components/provider-tools/ImportProviderToolsModal';
import { notify } from '@/lib/notifications';
import { useAdminClient } from '@/lib/client/adminClient';
import { downloadBlob } from '@/lib/utils/export';

export default function ProviderToolsPage() {
  const { executeWithAdmin } = useAdminClient();
  const [refreshKey, setRefreshKey] = useState(0);
  const [createToolOpen, setCreateToolOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  const handleExport = async () => {
    try {
      const tools = await executeWithAdmin(client =>
        client.providerTools.exportProviderTools()
      );
      
      const blob = new Blob([JSON.stringify(tools, null, 2)], { type: 'application/json' });
      downloadBlob(blob, `provider-tools-${new Date().toISOString().split('T')[0]}.json`);


      notify.success(`Exported ${(tools as unknown[]).length} provider tools`, 'Export Successful');
    } catch (error) {
      console.error('Failed to export tools:', error);
      notify.error(error, 'Failed to export provider tools');
    }
  };

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Provider Tools</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure tool costs for different providers
            </Text>
          </div>
          <Group gap="xs">
            <Button
              variant="subtle"
              onClick={() => void handleExport()}
            >
              Export
            </Button>
            <Button
              variant="subtle"
              onClick={() => setImportOpen(true)}
            >
              Import
            </Button>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="subtle"
              onClick={handleRefresh}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={() => setCreateToolOpen(true)}
            >
              Add Tool
            </Button>
          </Group>
        </Group>

        <ProviderToolsTable 
          key={`tools-${refreshKey}`}
          onRefresh={handleRefresh}
        />
      </Stack>

      <CreateProviderToolModal
        isOpen={createToolOpen}
        onClose={() => setCreateToolOpen(false)}
        onSuccess={() => {
          setCreateToolOpen(false);
          handleRefresh();
        }}
      />

      <ImportProviderToolsModal
        isOpen={importOpen}
        onClose={() => setImportOpen(false)}
        onSuccess={handleRefresh}
      />
    </Container>
  );
}
