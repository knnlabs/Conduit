'use client';

import {
  Modal,
  Stack,
  Group,
  Text,
  Badge,
  Table,
  ScrollArea,
  LoadingOverlay,
  Alert,
  Pagination,
  Card,
} from '@mantine/core';
import {
  IconHistory,
  IconCash,
  IconAlertCircle,
  IconArrowUp,
  IconArrowDown,
  IconRefresh,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { formatters } from '@/lib/utils/formatters';
import { withAdminClient } from '@/lib/client/adminClient';
import {
  TransactionType,
  ReferenceType,
  type VirtualKeyGroupDto,
  type VirtualKeyGroupTransactionDto,
} from '@/lib/admin-api';

interface TransactionHistoryModalProps {
  opened: boolean;
  onClose: () => void;
  group: VirtualKeyGroupDto | null;
}

const ITEMS_PER_PAGE = 50; // Match API default

export function TransactionHistoryModal({ opened, onClose, group }: TransactionHistoryModalProps) {
  const [transactions, setTransactions] = useState<VirtualKeyGroupTransactionDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  useEffect(() => {
    if (opened && group) {
      void fetchTransactions(1);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened, group?.id]);

  const fetchTransactions = async (pageNumber: number) => {
    if (!group) return;

    try {
      setIsLoading(true);
      setError(null);

      const data = await withAdminClient(client =>
        client.virtualKeyGroups.getTransactionHistory(
          group.id,
          {
            page: pageNumber,
            pageSize: ITEMS_PER_PAGE
          }
        )
      );

      setTransactions(data.data ?? []);
      setTotalPages(data.pagination?.totalPages ?? 1);
      setTotalCount(data.pagination?.totalItems ?? 0);
      setPage(pageNumber);
    } catch (error) {
      console.error('Failed to fetch transactions:', error);
      setError(error instanceof Error ? error.message : 'Failed to fetch transaction history');
    } finally {
      setIsLoading(false);
    }
  };

  const getTransactionIcon = (type: TransactionType) => {
    switch (type) {
      case TransactionType.Credit:
        return <IconArrowUp size={16} />;
      case TransactionType.Debit:
        return <IconArrowDown size={16} />;
      case TransactionType.Refund:
        return <IconRefresh size={16} />;
      case TransactionType.Adjustment:
        return <IconCash size={16} />;
      default:
        return <IconCash size={16} />;
    }
  };

  const getTransactionColor = (type: TransactionType) => {
    switch (type) {
      case TransactionType.Credit:
      case TransactionType.Refund:
      case TransactionType.Adjustment:
        return 'green';
      case TransactionType.Debit:
        return 'red';
      default:
        return 'gray';
    }
  };

  const getBalanceColor = (balance: number) => {
    if (balance <= 0) return 'red';
    if (balance < 10) return 'orange';
    return 'green';
  };

  const getTransactionTypeLabel = (type: TransactionType): string => {
    switch (type) {
      case TransactionType.Credit: return 'Credit';
      case TransactionType.Debit: return 'Debit';
      case TransactionType.Refund: return 'Refund';
      case TransactionType.Adjustment: return 'Adjustment';
      default: return 'Unknown';
    }
  };

  const getReferenceTypeLabel = (type: ReferenceType): string => {
    switch (type) {
      case ReferenceType.Manual: return 'Manual';
      case ReferenceType.VirtualKey: return 'Virtual Key';
      case ReferenceType.System: return 'System';
      case ReferenceType.Initial: return 'Initial';
      default: return 'Unknown';
    }
  };

  if (!group) return null;

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={
        <Group gap="sm">
          <IconHistory size={20} />
          <Text fw={500}>Transaction History</Text>
        </Group>
      }
      size="xl"
    >
      <Stack gap="lg">
        {/* Group Information */}
        <Card withBorder>
          <Group justify="space-between">
            <div>
              <Text size="sm" c="dimmed">Group</Text>
              <Text fw={500}>{group.groupName}</Text>
            </div>
            <div>
              <Text size="sm" c="dimmed">Current Balance</Text>
              <Badge 
                color={getBalanceColor(group.balance)} 
                variant={group.balance <= 0 ? 'filled' : 'light'}
                size="lg"
              >
                {formatters.currency(group.balance)}
              </Badge>
            </div>
            <div>
              <Text size="sm" c="dimmed">Lifetime Credits</Text>
              <Text size="sm">{formatters.currency(group.lifetimeCreditsAdded)}</Text>
            </div>
            <div>
              <Text size="sm" c="dimmed">Lifetime Spent</Text>
              <Text size="sm">{formatters.currency(group.lifetimeSpent)}</Text>
            </div>
          </Group>
        </Card>

        {/* Transactions Table */}
        <div style={{ position: 'relative' }}>
          <LoadingOverlay visible={isLoading} />
          
          {totalCount > 0 && !isLoading && (
            <Text size="sm" c="dimmed" mb="sm">
              Showing {transactions.length} of {totalCount} transactions
            </Text>
          )}

          {error && (
            <Alert icon={<IconAlertCircle size={16} />} color="red">
              {error}
            </Alert>
          )}
          
          {!error && transactions.length > 0 && (
            <>
              <ScrollArea h={400}>
                <Table verticalSpacing="sm">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Date & Time</Table.Th>
                      <Table.Th>Type</Table.Th>
                      <Table.Th>Description</Table.Th>
                      <Table.Th>Amount</Table.Th>
                      <Table.Th>Balance After</Table.Th>
                      <Table.Th>Initiated By</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {transactions.map((transaction) => (
                      <Table.Tr key={transaction.id}>
                        <Table.Td>
                          <Text size="sm">{formatters.date(transaction.createdAt, { includeTime: true })}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Badge 
                            leftSection={getTransactionIcon(transaction.transactionType)}
                            color={getTransactionColor(transaction.transactionType)}
                            variant="light"
                          >
                            {getTransactionTypeLabel(transaction.transactionType)}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{transaction.description ?? '-'}</Text>
                          {transaction.referenceType && (
                            <Text size="xs" c="dimmed">
                              {getReferenceTypeLabel(transaction.referenceType)}
                              {transaction.referenceId && ` #${transaction.referenceId}`}
                            </Text>
                          )}
                        </Table.Td>
                        <Table.Td>
                          <Text 
                            size="sm" 
                            fw={500}
                            c={transaction.amount >= 0 ? 'green' : 'red'}
                          >
                            {transaction.transactionType === TransactionType.Credit ||
                            transaction.transactionType === TransactionType.Refund ? '+' : '-'}
                            {formatters.currency(transaction.amount)}
                          </Text>
                        </Table.Td>
                        <Table.Td>
                          <Badge 
                            color={getBalanceColor(transaction.balanceAfter)} 
                            variant="light"
                          >
                            {formatters.currency(transaction.balanceAfter)}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Text size="sm">{transaction.initiatedBy ?? 'System'}</Text>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              </ScrollArea>

              {totalPages > 1 && (
                <Group justify="center" mt="md">
                  <Pagination
                    total={totalPages}
                    value={page}
                    onChange={(value) => void fetchTransactions(value)}
                  />
                </Group>
              )}
            </>
          )}
          
          {!error && transactions.length === 0 && (
            <Text c="dimmed" ta="center" py="xl">
              No transaction history available
            </Text>
          )}
        </div>
      </Stack>
    </Modal>
  );
}
