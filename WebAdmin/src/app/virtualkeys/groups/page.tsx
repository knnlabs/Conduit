'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  TextInput,
  Table,
  Badge,
  ActionIcon,
  Menu,
  rem,
  LoadingOverlay,
  Alert,
  SimpleGrid,
  ThemeIcon,
} from '@mantine/core';
import {
  IconPlus,
  IconSearch,
  IconCreditCard,
  IconAlertCircle,
  IconEye,
  IconDotsVertical,
  IconCash,
  IconLayersLinked,
  IconHistory,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useSearchParams } from 'next/navigation';
import { formatters } from '@/lib/utils/formatters';
import type { VirtualKeyGroupDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';

// Import modals lazily
import { 
  LazyCreateVirtualKeyGroupModal as CreateVirtualKeyGroupModal,
  LazyViewVirtualKeyGroupModal as ViewVirtualKeyGroupModal,
  LazyAddCreditsModal as AddCreditsModal,
  LazyTransactionHistoryModal as TransactionHistoryModal,
} from '@/components/lazy/LazyModals';

export default function VirtualKeyGroupsPage() {
  const [groups, setGroups] = useState<VirtualKeyGroupDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGroup, setSelectedGroup] = useState<VirtualKeyGroupDto | null>(null);
  const searchParams = useSearchParams();

  // Modal states
  const [createModalOpened, { open: openCreateModal, close: closeCreateModal }] = useDisclosure(false);
  const [viewModalOpened, { open: openViewModal, close: closeViewModal }] = useDisclosure(false);
  const [creditsModalOpened, { open: openCreditsModal, close: closeCreditsModal }] = useDisclosure(false);
  const [transactionHistoryOpened, { open: openTransactionHistory, close: closeTransactionHistory }] = useDisclosure(false);

  // Fetch groups on mount
  useEffect(() => {
    void fetchGroups();
  }, []);

  // Handle groupId parameter to auto-open modal
  useEffect(() => {
    const groupIdParam = searchParams.get('groupId');
    if (groupIdParam && groups.length > 0) {
      const groupId = parseInt(groupIdParam, 10);
      const group = groups.find(g => g.id === groupId);
      if (group) {
        setSelectedGroup(group);
        openViewModal();
      }
    }
  }, [searchParams, groups, openViewModal]);

  const fetchGroups = async () => {
    try {
      setIsLoading(true);
      setError(null);
      
      const data = await withAdminClient(client => 
        client.virtualKeyGroups.list()
      );
      setGroups(data);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Unknown error'));
    } finally {
      setIsLoading(false);
    }
  };

  // Filter groups based on search
  const filteredGroups = groups.filter((group) => {
    if (!searchQuery) return true;
    
    const query = searchQuery.toLowerCase();
    return (
      group.groupName.toLowerCase().includes(query) ||
      group.id.toString().includes(query) ||
      (group.externalGroupId?.toLowerCase().includes(query) ?? false)
    );
  });

  // Calculate statistics
  const stats = {
    totalGroups: filteredGroups.length,
    totalBalance: filteredGroups.reduce((sum, g) => sum + g.balance, 0),
    zeroBalanceGroups: filteredGroups.filter(g => g.balance <= 0).length,
  };

  const handleViewGroup = (group: VirtualKeyGroupDto) => {
    setSelectedGroup(group);
    openViewModal();
  };

  const handleAddCredits = (group: VirtualKeyGroupDto) => {
    setSelectedGroup(group);
    openCreditsModal();
  };

  const handleViewTransactionHistory = (group: VirtualKeyGroupDto) => {
    setSelectedGroup(group);
    openTransactionHistory();
  };

  const getBalanceColor = (balance: number) => {
    if (balance <= 0) return 'red';
    if (balance < 10) return 'orange';
    return 'green';
  };

  const statCards = [
    {
      title: 'Total Groups',
      value: stats.totalGroups,
      icon: IconLayersLinked,
      color: 'blue',
    },
    {
      title: 'Total Balance',
      value: formatters.currency(stats.totalBalance, { precision: 2 }),
      icon: IconCreditCard,
      color: 'green',
    },
    {
      title: 'Zero Balance Groups',
      value: stats.zeroBalanceGroups,
      icon: IconAlertCircle,
      color: 'red',
    },
  ];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>Virtual Key Groups</Title>
          <Text c="dimmed">Manage balance and credits for virtual key groups</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading virtual key groups"
          color="red"
        >
          {error.message}
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>Virtual Key Groups</Title>
          <Text c="dimmed">Manage balance and credits for virtual key groups</Text>
        </div>

        <Button
          leftSection={<IconPlus size={16} />}
          onClick={openCreateModal}
        >
          Create Group
        </Button>
      </Group>

      {/* Statistics Cards */}
      <SimpleGrid cols={{ base: 1, sm: 3 }} spacing="lg">
        {statCards.map((stat) => (
          <Card key={stat.title} p="md" withBorder>
            <Group justify="space-between">
              <div>
                <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                  {stat.title}
                </Text>
                <Text fw={700} size="xl">
                  {stat.value}
                </Text>
              </div>
              <ThemeIcon size="lg" variant="light" color={stat.color}>
                <stat.icon size={20} />
              </ThemeIcon>
            </Group>
          </Card>
        ))}
      </SimpleGrid>

      {/* Groups Table */}
      <Card>
        <Card.Section p="md" withBorder>
          <Group justify="space-between">
            <Text fw={600}>Groups</Text>
            <TextInput
              placeholder="Search by name or ID..."
              leftSection={<IconSearch size={16} />}
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.currentTarget.value)}
              w={300}
            />
          </Group>
        </Card.Section>

        <Card.Section p="md" pt={0} style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          
          <Table verticalSpacing="sm">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Group Name</Table.Th>
                <Table.Th>Group ID</Table.Th>
                <Table.Th>Current Balance</Table.Th>
                <Table.Th>Lifetime Credits</Table.Th>
                <Table.Th>Lifetime Spent</Table.Th>
                <Table.Th>Keys</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {filteredGroups.length > 0 ? (
                filteredGroups.map((group) => (
                  <Table.Tr key={group.id}>
                    <Table.Td fw={500}>{group.groupName}</Table.Td>
                    <Table.Td>
                      <Stack gap={2}>
                        <Text size="sm">#{group.id}</Text>
                        {group.externalGroupId && (
                          <Text size="xs" c="dimmed">
                            Ext: {group.externalGroupId}
                          </Text>
                        )}
                      </Stack>
                    </Table.Td>
                    <Table.Td>
                      <Badge 
                        color={getBalanceColor(group.balance)} 
                        variant={group.balance <= 0 ? 'filled' : 'light'}
                      >
                        {formatters.currency(group.balance)}
                      </Badge>
                    </Table.Td>
                    <Table.Td>{formatters.currency(group.lifetimeCreditsAdded)}</Table.Td>
                    <Table.Td>{formatters.currency(group.lifetimeSpent)}</Table.Td>
                    <Table.Td>
                      <Badge color="gray" variant="light">
                        {group.virtualKeyCount} {group.virtualKeyCount === 1 ? 'key' : 'keys'}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      <Group gap={0} justify="flex-end">
                        <Menu position="bottom-end" withinPortal>
                          <Menu.Target>
                            <ActionIcon variant="subtle" color="gray" size="sm">
                              <IconDotsVertical style={{ width: rem(16), height: rem(16) }} />
                            </ActionIcon>
                          </Menu.Target>
                          <Menu.Dropdown>
                            <Menu.Item
                              leftSection={<IconEye style={{ width: rem(14), height: rem(14) }} />}
                              onClick={() => handleViewGroup(group)}
                            >
                              View Details
                            </Menu.Item>
                            <Menu.Item
                              leftSection={<IconHistory style={{ width: rem(14), height: rem(14) }} />}
                              onClick={() => handleViewTransactionHistory(group)}
                            >
                              Transaction History
                            </Menu.Item>
                            <Menu.Item
                              leftSection={<IconCash style={{ width: rem(14), height: rem(14) }} />}
                              onClick={() => handleAddCredits(group)}
                              color={group.balance <= 0 ? 'red' : undefined}
                            >
                              Add Credits
                            </Menu.Item>
                          </Menu.Dropdown>
                        </Menu>
                      </Group>
                    </Table.Td>
                  </Table.Tr>
                ))
              ) : (
                <Table.Tr>
                  <Table.Td colSpan={7}>
                    <Text ta="center" py="xl" c="dimmed">
                      {searchQuery ? 'No groups found matching your search' : 'No virtual key groups found'}
                    </Text>
                  </Table.Td>
                </Table.Tr>
              )}
            </Table.Tbody>
          </Table>
        </Card.Section>
      </Card>

      {/* Modals */}
      <CreateVirtualKeyGroupModal
        opened={createModalOpened}
        onClose={closeCreateModal}
        onSuccess={() => void fetchGroups()}
      />

      <ViewVirtualKeyGroupModal
        opened={viewModalOpened}
        onClose={closeViewModal}
        group={selectedGroup}
      />

      <AddCreditsModal
        opened={creditsModalOpened}
        onClose={closeCreditsModal}
        group={selectedGroup}
        onSuccess={() => void fetchGroups()}
      />

      <TransactionHistoryModal
        opened={transactionHistoryOpened}
        onClose={closeTransactionHistory}
        group={selectedGroup}
      />
    </Stack>
  );
}