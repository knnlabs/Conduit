'use client';

import { useState } from 'react';
import { Container, Title, Text, Button, Group, Stack } from '@mantine/core';
import { IconPlus, IconRefresh, IconFileImport, IconFileExport } from '@tabler/icons-react';
import { ModelCostsTable } from './components/ModelCostsTable';
import { CreateModelCostModal } from './components/CreateModelCostModal';
import { ImportModelCostsModal } from './components/ImportModelCostsModal';
import { useModelCostsApi } from './hooks/useModelCostsApi';

export default function ModelCostsPage() {
  const [refreshKey, setRefreshKey] = useState(0);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [importModalOpen, setImportModalOpen] = useState(false);
  const { exportModelCosts, isExporting } = useModelCostsApi();

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  const handleExport = async () => {
    try {
      await exportModelCosts();
    } catch (error) {
      // Error handling is done in the hook
    }
  };

  return (
    <Container size="xl">
      <Stack gap="md">
        <Group justify="space-between" align="flex-end">
          <div>
            <Title order={2}>Model Pricing Configuration</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure pricing for LLM models to enable accurate cost tracking
            </Text>
          </div>
          <Group>
            <Button
              leftSection={<IconFileExport size={16} />}
              variant="light"
              onClick={handleExport}
              loading={isExporting}
            >
              Export CSV
            </Button>
            <Button
              leftSection={<IconFileImport size={16} />}
              variant="light"
              onClick={() => setImportModalOpen(true)}
            >
              Import CSV
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
              onClick={() => setCreateModalOpen(true)}
            >
              Add Pricing
            </Button>
          </Group>
        </Group>

        <ModelCostsTable 
          key={refreshKey} 
          onRefresh={handleRefresh}
        />
      </Stack>

      <CreateModelCostModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onSuccess={() => {
          setCreateModalOpen(false);
          handleRefresh();
        }}
      />
      
      <ImportModelCostsModal
        isOpen={importModalOpen}
        onClose={() => setImportModalOpen(false)}
        onSuccess={() => {
          setImportModalOpen(false);
          handleRefresh();
        }}
      />
    </Container>
  );
}