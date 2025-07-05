'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Table,
  ScrollArea,
  Badge,
  Group,
  Button,
  TextInput,
  NumberInput,
  Select,
  Modal,
  ActionIcon,
  Switch,
  Grid,
  ThemeIcon,
  Paper,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconCoin,
  IconEdit,
  IconTrash,
  IconPlus,
  IconSearch,
  IconFilter,
  IconDownload,
  IconUpload,
  IconCalculator,
  IconTrendingUp,
  IconCheck,
  IconRefresh,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
// Removed unused formatCurrency and formatNumber imports
import { 
  useModelCosts, 
  useCreateModelCost, 
  useUpdateModelCost, 
  useDeleteModelCost,
  useModelCostOverview,
  type ModelCost,
  type CreateModelCost,
  type ModelCostOverview,
} from '@/hooks/api/useModelCostsApi';


const categoryColors = {
  text: 'blue',
  embedding: 'cyan',
  image: 'green',
  audio: 'orange',
  video: 'red',
};

const _categoryIcons = {
  text: IconCoin,
  embedding: IconCalculator,
  image: IconCoin,
  audio: IconCoin,
  video: IconCoin,
};

export default function ModelCostsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [categoryFilter, setCategoryFilter] = useState<string>('all');
  const [providerFilter, setProviderFilter] = useState<string>('all');
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [editingCost, setEditingCost] = useState<ModelCost | null>(null);
  
  // Fetch data using the model costs API hooks
  const { data: costs = [], isLoading: costsLoading, refetch: refetchCosts } = useModelCosts();
  const createMutation = useCreateModelCost();
  const updateMutation = useUpdateModelCost();
  const deleteMutation = useDeleteModelCost();
  
  // Get cost overview for time period
  const today = new Date();
  const startDate = new Date(today.getFullYear(), today.getMonth(), 1).toISOString(); // Start of month
  const endDate = today.toISOString();
  const { data: overviewData } = useModelCostOverview(startDate, endDate);

  // Form state
  const [formData, setFormData] = useState<CreateModelCost>({
    modelIdPattern: '',
    providerName: '',
    inputCostPerMillionTokens: 0,
    outputCostPerMillionTokens: 0,
    isActive: true,
    priority: 0,
    effectiveDate: new Date().toISOString(),
    description: '',
    modelCategory: 'text',
  });

  // Filter costs
  const filteredCosts = costs.filter((cost: ModelCost) => {
    const matchesSearch = 
      cost.modelIdPattern.toLowerCase().includes(searchQuery.toLowerCase()) ||
      cost.providerName.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesCategory = categoryFilter === 'all' || cost.modelCategory === categoryFilter;
    const matchesProvider = providerFilter === 'all' || cost.providerName === providerFilter;
    
    return matchesSearch && matchesCategory && matchesProvider;
  });

  // Get unique providers
  const providers: string[] = Array.from(new Set(costs.map((c: ModelCost) => c.providerName)));

  // Calculate statistics
  const totalModels = costs.length;
  const activeModels = costs.filter((c: ModelCost) => c.isActive).length;
  const totalSpend = overviewData?.reduce((sum: number, item: ModelCostOverview) => sum + item.totalCost, 0) || 0;

  const handleAddCost = () => {
    setEditingCost(null);
    setFormData({
      modelIdPattern: '',
      providerName: '',
      inputCostPerMillionTokens: 0,
      outputCostPerMillionTokens: 0,
      isActive: true,
      priority: 0,
      effectiveDate: new Date().toISOString(),
      description: '',
      modelCategory: 'text',
    });
    openModal();
  };

  const handleEditCost = (cost: ModelCost) => {
    setEditingCost(cost);
    setFormData({
      modelIdPattern: cost.modelIdPattern,
      providerName: cost.providerName,
      inputCostPerMillionTokens: cost.inputCostPerMillionTokens,
      outputCostPerMillionTokens: cost.outputCostPerMillionTokens,
      isActive: cost.isActive,
      priority: cost.priority,
      effectiveDate: cost.effectiveDate,
      description: cost.description || '',
      modelCategory: cost.modelCategory || 'text',
    });
    openModal();
  };

  const handleSaveCost = async () => {
    try {
      if (editingCost) {
        await updateMutation.mutateAsync({
          ...formData,
          id: editingCost.id,
        });
        notifications.show({
          title: 'Cost Updated',
          message: `${formData.modelIdPattern} pricing has been updated`,
          color: 'green',
        });
      } else {
        await createMutation.mutateAsync(formData);
        notifications.show({
          title: 'Cost Added',
          message: `${formData.modelIdPattern} pricing has been added`,
          color: 'green',
        });
      }
      closeModal();
    } catch (_error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to save model cost',
        color: 'red',
      });
    }
  };

  const handleDeleteCost = async (id: number) => {
    try {
      await deleteMutation.mutateAsync(id);
      notifications.show({
        title: 'Cost Deleted',
        message: 'Model pricing has been removed',
        color: 'red',
      });
    } catch (_error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to delete model cost',
        color: 'red',
      });
    }
  };

  const handleToggleActive = async (cost: ModelCost) => {
    try {
      await updateMutation.mutateAsync({
        ...cost,
        isActive: !cost.isActive,
      });
    } catch (_error) {
      notifications.show({
        title: 'Error',
        message: 'Failed to update model cost',
        color: 'red',
      });
    }
  };

  const handleExport = () => {
    notifications.show({
      title: 'Export Started',
      message: 'Exporting pricing configuration...',
      color: 'blue',
    });
  };

  const handleImport = () => {
    notifications.show({
      title: 'Import',
      message: 'Select a pricing configuration file to import',
      color: 'blue',
    });
  };

  const handleRefresh = () => {
    refetchCosts();
    notifications.show({
      title: 'Refreshing Prices',
      message: 'Model pricing data is being updated',
      color: 'blue',
    });
  };

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={1}>Model Pricing</Title>
          <Text c="dimmed">Configure pricing and markup for AI models</Text>
        </div>
        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleRefresh}
          >
            Refresh Prices
          </Button>
          <Button
            variant="light"
            leftSection={<IconUpload size={16} />}
            onClick={handleImport}
          >
            Import
          </Button>
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
          >
            Export
          </Button>
          <Button
            leftSection={<IconPlus size={16} />}
            onClick={handleAddCost}
          >
            Add Pricing
          </Button>
        </Group>
      </Group>

      {/* Statistics */}
      <Grid>
        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Models
                </Text>
                <Text size="xl" fw={700}>
                  {totalModels}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light">
                <IconCalculator size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Active Models
                </Text>
                <Text size="xl" fw={700}>
                  {activeModels}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="green">
                <IconCheck size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Total Spend
                </Text>
                <Text size="xl" fw={700}>
                  ${totalSpend.toFixed(2)}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="orange">
                <IconTrendingUp size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
          <Card withBorder p="md">
            <Group justify="space-between">
              <div>
                <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                  Providers
                </Text>
                <Text size="xl" fw={700}>
                  {providers.length}
                </Text>
              </div>
              <ThemeIcon size="xl" radius="md" variant="light" color="blue">
                <IconCoin size={24} />
              </ThemeIcon>
            </Group>
          </Card>
        </Grid.Col>
      </Grid>

      {/* Filters */}
      <Card withBorder>
        <Grid>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <TextInput
              placeholder="Search models or providers..."
              leftSection={<IconSearch size={16} />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.currentTarget.value)}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <Select
              placeholder="Category"
              data={[
                { value: 'all', label: 'All Categories' },
                { value: 'text', label: 'Text Generation' },
                { value: 'embedding', label: 'Embeddings' },
                { value: 'image', label: 'Image Generation' },
                { value: 'audio', label: 'Audio Processing' },
                { value: 'video', label: 'Video Generation' },
              ]}
              value={categoryFilter}
              onChange={(value) => setCategoryFilter(value || 'all')}
              leftSection={<IconFilter size={16} />}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 4 }}>
            <Select
              placeholder="Provider"
              data={[
                { value: 'all', label: 'All Providers' },
                ...providers.map(p => ({ value: p, label: p })),
              ]}
              value={providerFilter}
              onChange={(value) => setProviderFilter(value || 'all')}
              leftSection={<IconFilter size={16} />}
            />
          </Grid.Col>
        </Grid>
      </Card>

      {/* Pricing Table */}
      <Card withBorder>
        <LoadingOverlay visible={costsLoading} />
        <ScrollArea>
          <Table striped highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Model Pattern</Table.Th>
                <Table.Th>Provider</Table.Th>
                <Table.Th>Category</Table.Th>
                <Table.Th>Input Cost</Table.Th>
                <Table.Th>Output Cost</Table.Th>
                <Table.Th>Priority</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filteredCosts.map((cost: ModelCost) => {
                return (
                  <Table.Tr key={cost.id}>
                    <Table.Td>
                      <div>
                        <Text fw={500}>{cost.modelIdPattern}</Text>
                        {cost.description && (
                          <Text size="xs" c="dimmed">{cost.description}</Text>
                        )}
                      </div>
                    </Table.Td>
                    <Table.Td>
                      <Text size="sm">{cost.providerName}</Text>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light" color={categoryColors[cost.modelCategory as keyof typeof categoryColors] || 'gray'}>
                        {cost.modelCategory || 'unknown'}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <div>
                        <Text size="sm">
                          ${cost.inputCostPerMillionTokens.toFixed(2)}
                        </Text>
                        <Text size="xs" c="dimmed">
                          per 1M tokens
                        </Text>
                      </div>
                    </Table.Td>
                    <Table.Td>
                      {cost.outputCostPerMillionTokens > 0 ? (
                        <div>
                          <Text size="sm">
                            ${cost.outputCostPerMillionTokens.toFixed(2)}
                          </Text>
                          <Text size="xs" c="dimmed">
                            per 1M tokens
                          </Text>
                        </div>
                      ) : (
                        <Text size="sm" c="dimmed">N/A</Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light" color="blue">
                        {cost.priority}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Switch
                        checked={cost.isActive}
                        onChange={() => handleToggleActive(cost)}
                        size="sm"
                      />
                    </Table.Td>
                    <Table.Td>
                      <Group gap="xs">
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          onClick={() => handleEditCost(cost)}
                        >
                          <IconEdit size={16} />
                        </ActionIcon>
                        <ActionIcon
                          variant="subtle"
                          size="sm"
                          color="red"
                          onClick={() => handleDeleteCost(cost.id)}
                        >
                          <IconTrash size={16} />
                        </ActionIcon>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                );
              })}
            </Table.Tbody>
          </Table>
        </ScrollArea>
      </Card>

      {/* Pricing Calculator */}
      <Card withBorder>
        <Text fw={600} mb="md">Quick Cost Calculator</Text>
        <Grid>
          <Grid.Col span={{ base: 12, md: 3 }}>
            <Select
              label="Model"
              placeholder="Select a model"
              data={costs.map((c: ModelCost) => ({
                value: c.id.toString(),
                label: `${c.modelIdPattern} (${c.providerName})`,
              }))}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 3 }}>
            <NumberInput
              label="Input Tokens (Millions)"
              placeholder="Enter amount"
              min={0}
              decimalScale={3}
              thousandSeparator=","
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 3 }}>
            <NumberInput
              label="Output Tokens (Millions)"
              placeholder="Enter amount"
              min={0}
              decimalScale={3}
              thousandSeparator=","
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, md: 3 }}>
            <Paper p="md" withBorder h="100%" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <div style={{ textAlign: 'center' }}>
                <Text size="xs" c="dimmed">Estimated Cost</Text>
                <Text size="xl" fw={700} c="green">$0.00</Text>
              </div>
            </Paper>
          </Grid.Col>
        </Grid>
      </Card>

      {/* Add/Edit Modal */}
      <Modal
        opened={modalOpened}
        onClose={closeModal}
        title={editingCost ? 'Edit Model Pricing' : 'Add Model Pricing'}
        size="lg"
      >
        <Stack gap="md">
          <Grid>
            <Grid.Col span={6}>
              <TextInput
                label="Provider Name"
                placeholder="e.g., OpenAI"
                value={formData.providerName}
                onChange={(e) => setFormData({ ...formData, providerName: e.currentTarget.value })}
                required
              />
            </Grid.Col>
            <Grid.Col span={6}>
              <TextInput
                label="Model ID Pattern"
                placeholder="e.g., gpt-4*"
                value={formData.modelIdPattern}
                onChange={(e) => setFormData({ ...formData, modelIdPattern: e.currentTarget.value })}
                required
              />
            </Grid.Col>
          </Grid>

          <Select
            label="Category"
            data={[
              { value: 'text', label: 'Text Generation' },
              { value: 'embedding', label: 'Embeddings' },
              { value: 'image', label: 'Image Generation' },
              { value: 'audio', label: 'Audio Processing' },
              { value: 'video', label: 'Video Generation' },
            ]}
            value={formData.modelCategory}
            onChange={(value) => setFormData({ ...formData, modelCategory: value || 'text' })}
            required
          />

          <Grid>
            <Grid.Col span={6}>
              <NumberInput
                label="Input Cost per Million Tokens"
                placeholder="0.00"
                value={formData.inputCostPerMillionTokens}
                onChange={(value) => setFormData({ ...formData, inputCostPerMillionTokens: Number(value) || 0 })}
                decimalScale={2}
                min={0}
                required
              />
            </Grid.Col>
            <Grid.Col span={6}>
              <NumberInput
                label="Output Cost per Million Tokens"
                placeholder="0.00"
                value={formData.outputCostPerMillionTokens}
                onChange={(value) => setFormData({ ...formData, outputCostPerMillionTokens: Number(value) || 0 })}
                decimalScale={2}
                min={0}
              />
            </Grid.Col>
          </Grid>

          <Grid>
            <Grid.Col span={6}>
              <NumberInput
                label="Priority"
                placeholder="0"
                value={formData.priority}
                onChange={(value) => setFormData({ ...formData, priority: Number(value) || 0 })}
                min={0}
                max={100}
                required
              />
            </Grid.Col>
            <Grid.Col span={6}>
              <TextInput
                label="Effective Date"
                value={new Date(formData.effectiveDate || new Date()).toLocaleDateString()}
                readOnly
              />
            </Grid.Col>
          </Grid>

          <TextInput
            label="Description"
            placeholder="Additional pricing information..."
            value={formData.description}
            onChange={(e) => setFormData({ ...formData, description: e.currentTarget.value })}
          />

          <Switch
            label="Active"
            description="Enable this pricing configuration"
            checked={formData.isActive}
            onChange={(e) => setFormData({ ...formData, isActive: e.currentTarget.checked })}
          />

          <Group justify="flex-end" mt="md">
            <Button variant="light" onClick={closeModal}>
              Cancel
            </Button>
            <Button onClick={handleSaveCost} loading={createMutation.isPending || updateMutation.isPending}>
              {editingCost ? 'Update Pricing' : 'Add Pricing'}
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}