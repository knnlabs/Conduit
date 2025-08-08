'use client';

import { useParams, useRouter } from 'next/navigation';
import { useState, useEffect, useCallback } from 'react';
import {
  Container,
  Title,
  Text,
  Stack,
  Group,
  Button,
  Badge,
  ActionIcon,
  TextInput,
  Switch,
  Card,
  Menu,
  rem,
  Alert,
  LoadingOverlay,
  Breadcrumbs,
  Anchor,
  Paper,
} from '@mantine/core';
import {
  IconKey,
  IconPlus,
  IconTrash,
  IconStar,
  IconStarFilled,
  IconDotsVertical,
  IconAlertCircle,
  IconTestPipe,
  IconArrowLeft,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { modals } from '@mantine/modals';
import type { ProviderCredentialDto, ProviderKeyCredentialDto, CreateProviderKeyCredentialDto } from '@knn_labs/conduit-admin-client';
import { withAdminClient } from '@/lib/client/adminClient';
import { formatters } from '@/lib/utils/formatters';
import { getProviderDisplayName } from '@/lib/utils/providerTypeUtils';
import Link from 'next/link';

export default function ProviderKeysPage() {
  const params = useParams();
  const router = useRouter();
  const providerId = Number(params.id);
  
  const [provider, setProvider] = useState<ProviderCredentialDto | null>(null);
  const [keys, setKeys] = useState<ProviderKeyCredentialDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isAddingKey, setIsAddingKey] = useState(false);
  const [testingKeys, setTestingKeys] = useState<Set<number>>(new Set());
  const [showAddForm, setShowAddForm] = useState(false);
  const [newKeyForm, setNewKeyForm] = useState<CreateProviderKeyCredentialDto>({
    apiKey: '',
    keyName: '',
    organization: '',
    isPrimary: false,
    isEnabled: true,
  });

  const fetchProvider = useCallback(async () => {
    try {
      const data = await withAdminClient(client => 
        client.providers.getById(providerId)
      );
      setProvider(data as ProviderCredentialDto);
    } catch (error) {
      console.error('Error fetching provider:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load provider details',
        color: 'red',
      });
    }
  }, [providerId]);

  const fetchKeys = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await withAdminClient(client => 
        client.providers.listKeys(providerId)
) as unknown as ProviderKeyCredentialDto[];
      setKeys(data);
    } catch (error) {
      console.error('Error fetching provider keys:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to load provider keys',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  }, [providerId]);

  useEffect(() => {
    void fetchProvider();
    void fetchKeys();
  }, [fetchProvider, fetchKeys]);

  const handleAddKey = async () => {
    if (!newKeyForm.apiKey) return;

    try {
      setIsAddingKey(true);
      await withAdminClient(client => 
        client.providers.createKey(providerId, newKeyForm)
      );
      
      notifications.show({
        title: 'Success',
        message: 'Provider key added successfully',
        color: 'green',
      });
      
      // Reset form
      setNewKeyForm({
        apiKey: '',
        keyName: '',
        organization: '',
        isPrimary: false,
        isEnabled: true,
      });
      setShowAddForm(false);
      
      // Refresh keys
      void fetchKeys();
    } catch (error) {
      console.error('Error adding key:', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to add provider key';
      notifications.show({
        title: 'Error',
        message: errorMessage,
        color: 'red',
      });
    } finally {
      setIsAddingKey(false);
    }
  };

  const handleSetPrimary = async (keyId: number) => {
    try {
      await withAdminClient(client => 
        client.providers.setPrimaryKey(providerId, keyId)
      );
      
      notifications.show({
        title: 'Success',
        message: 'Primary key updated',
        color: 'green',
      });
      
      void fetchKeys();
    } catch (error) {
      console.error('Error setting primary key:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to set primary key',
        color: 'red',
      });
    }
  };

  const handleToggleKey = async (keyId: number, enabled: boolean) => {
    try {
      await withAdminClient(client => 
        client.providers.updateKey(providerId, keyId, { isEnabled: enabled })
      );
      
      notifications.show({
        title: 'Success',
        message: `Key ${enabled ? 'enabled' : 'disabled'} successfully`,
        color: 'green',
      });
      
      void fetchKeys();
    } catch (error) {
      console.error('Error updating key:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to update key',
        color: 'red',
      });
    }
  };

  const handleTestKey = async (keyId: number) => {
    setTestingKeys(prev => new Set(prev).add(keyId));
    try {
      const result = await withAdminClient(client => 
        client.providers.testKey(providerId, keyId)
      );
      
      // Handle new response format
      const isSuccess = (result.result as string) === 'success';
      const testResult = result.result as string;
      
      const colors: Record<string, string> = {
        'success': 'green',
        'invalid_key': 'red',
        'ignored': 'yellow',
        'provider_down': 'orange',
        'rate_limited': 'orange',
        'unknown_error': 'red'
      };
      
      notifications.show({
        title: isSuccess ? 'Key Test Successful' : 'Key Test Failed',
        message: result.message ?? (isSuccess ? 'The API key is valid and working' : 'The API key is invalid or not working'),
        color: colors[testResult] ?? 'red',
      });
    } catch (error) {
      console.error('Error testing key:', error);
      notifications.show({
        title: 'Error',
        message: 'Failed to test key',
        color: 'red',
      });
    } finally {
      setTestingKeys(prev => {
        const newSet = new Set(prev);
        newSet.delete(keyId);
        return newSet;
      });
    }
  };

  const handleDeleteKey = (key: ProviderKeyCredentialDto) => {
    if (key.isPrimary) {
      notifications.show({
        title: 'Cannot delete primary key',
        message: 'Please set another key as primary before deleting this one',
        color: 'red',
      });
      return;
    }

    modals.openConfirmModal({
      title: 'Delete Provider Key',
      children: (
        <Text size="sm">
          Are you sure you want to delete this key{key.keyName ? ` (${key.keyName})` : ''}? This action cannot be undone.
        </Text>
      ),
      labels: { confirm: 'Delete', cancel: 'Cancel' },
      confirmProps: { color: 'red' },
      onConfirm: () => {
        void (async () => {
          try {
            await withAdminClient(client => 
              client.providers.deleteKey(providerId, key.id)
            );
            
            notifications.show({
              title: 'Success',
              message: 'Key deleted successfully',
              color: 'green',
            });
            
            void fetchKeys();
          } catch (error) {
            console.error('Error deleting key:', error);
            notifications.show({
              title: 'Error',
              message: 'Failed to delete key',
              color: 'red',
            });
          }
        })();
      },
    });
  };

  return (
    <Container size="lg" py="xl">
      <LoadingOverlay visible={isLoading} zIndex={1000} overlayProps={{ radius: "sm", blur: 2 }} />
      
      <Breadcrumbs mb="xl">
        <Anchor component={Link} href="/llm-providers">
          LLM Providers
        </Anchor>
        <Text>{provider?.providerName ?? (provider?.providerType ? getProviderDisplayName(provider.providerType) : 'Loading...')}</Text>
        <Text>API Keys</Text>
      </Breadcrumbs>

      <Group justify="space-between" mb="xl">
        <div>
          <Title order={2}>API Keys for {provider?.providerName ?? (provider?.providerType ? getProviderDisplayName(provider.providerType) : '')} {provider?.id ? `(ID: ${provider.id})` : ''}</Title>
          <Text size="sm" c="dimmed" mt={4}>
            Manage multiple API keys for load balancing and failover
          </Text>
        </div>
        
        <Group>
          <Button
            leftSection={<IconArrowLeft size={16} />}
            variant="default"
            onClick={() => void router.push('/llm-providers')}
          >
            Back to Providers
          </Button>
          <Button
            leftSection={<IconPlus size={16} />}
            onClick={() => setShowAddForm(!showAddForm)}
            disabled={showAddForm}
          >
            Add New Key
          </Button>
        </Group>
      </Group>

      {showAddForm && (
        <Paper shadow="sm" p="lg" mb="xl" withBorder>
          <Title order={4} mb="md">Add New API Key</Title>
          <Stack>
            <TextInput
              label="API Key"
              placeholder="Enter API key"
              value={newKeyForm.apiKey}
              onChange={(e) => setNewKeyForm({ ...newKeyForm, apiKey: e.target.value })}
              required
            />
            
            <Group grow>
              <TextInput
                label="Key Name (optional)"
                placeholder="e.g., Production Key"
                value={newKeyForm.keyName}
                onChange={(e) => setNewKeyForm({ ...newKeyForm, keyName: e.target.value })}
              />
              
              <TextInput
                label="Organization (optional)"
                placeholder="e.g., OpenAI Org ID"
                value={newKeyForm.organization}
                onChange={(e) => setNewKeyForm({ ...newKeyForm, organization: e.target.value })}
              />
            </Group>
            
            <Group>
              <Switch
                label="Set as Primary"
                checked={newKeyForm.isPrimary}
                onChange={(e) => setNewKeyForm({ ...newKeyForm, isPrimary: e.currentTarget.checked })}
              />
              
              <Switch
                label="Enable Key"
                checked={newKeyForm.isEnabled}
                onChange={(e) => setNewKeyForm({ ...newKeyForm, isEnabled: e.currentTarget.checked })}
              />
            </Group>
            
            <Group>
              <Button
                onClick={() => void handleAddKey()}
                loading={isAddingKey}
                disabled={!newKeyForm.apiKey}
              >
                Add Key
              </Button>
              <Button
                variant="default"
                onClick={() => {
                  setShowAddForm(false);
                  setNewKeyForm({
                    apiKey: '',
                    keyName: '',
                    organization: '',
                    isPrimary: false,
                    isEnabled: true,
                  });
                }}
              >
                Cancel
              </Button>
            </Group>
          </Stack>
        </Paper>
      )}

      {keys.length === 0 && !isLoading ? (
        <Alert icon={<IconAlertCircle size={16} />} title="No API keys configured">
          Add API keys to enable multiple keys for load balancing and failover.
        </Alert>
      ) : (
        <Stack>
          {keys.map((key) => (
            <Card key={key.id} withBorder shadow="sm">
              <Group justify="space-between" wrap="nowrap">
                <Group>
                  <IconKey size={20} />
                  
                  <Stack gap={4}>
                    <Group gap="xs">
                      <Text fw={500}>
                        {key.keyName ?? `API Key ${key.id}`}
                      </Text>
                      {key.isPrimary && (
                        <Badge size="sm" variant="filled" leftSection={<IconStarFilled size={12} />}>
                          PRIMARY
                        </Badge>
                      )}
                      <Badge size="sm" variant={key.isEnabled ? 'light' : 'filled'} color={key.isEnabled ? 'green' : 'gray'}>
                        {key.isEnabled ? 'ENABLED' : 'DISABLED'}
                      </Badge>
                    </Group>
                    
                    <Group gap="xl">
                      <Text size="xs" c="dimmed">
                        API Key: {key.apiKey}
                      </Text>
                      {key.organization && (
                        <Text size="xs" c="dimmed">
                          Org: {key.organization}
                        </Text>
                      )}
                      <Text size="xs" c="dimmed">
                        Added: {formatters.date(key.createdAt)}
                      </Text>
                    </Group>
                  </Stack>
                </Group>
                
                <Group gap="xs">
                  <Switch
                    checked={key.isEnabled}
                    onChange={(e) => void handleToggleKey(key.id, e.currentTarget.checked)}
                    disabled={key.isPrimary && key.isEnabled}
                  />
                  
                  <Menu position="bottom-end" withinPortal>
                    <Menu.Target>
                      <ActionIcon variant="subtle" color="gray">
                        <IconDotsVertical style={{ width: rem(16), height: rem(16) }} />
                      </ActionIcon>
                    </Menu.Target>
                    <Menu.Dropdown>
                      <Menu.Item
                        leftSection={<IconTestPipe style={{ width: rem(14), height: rem(14) }} />}
                        onClick={() => void handleTestKey(key.id)}
                        disabled={testingKeys.has(key.id)}
                      >
                        {testingKeys.has(key.id) ? 'Testing...' : 'Test Key'}
                      </Menu.Item>
                      {!key.isPrimary && (
                        <Menu.Item
                          leftSection={<IconStar style={{ width: rem(14), height: rem(14) }} />}
                          onClick={() => void handleSetPrimary(key.id)}
                          disabled={!key.isEnabled}
                        >
                          Set as Primary
                        </Menu.Item>
                      )}
                      <Menu.Divider />
                      <Menu.Item
                        color="red"
                        leftSection={<IconTrash style={{ width: rem(14), height: rem(14) }} />}
                        onClick={() => handleDeleteKey(key)}
                        disabled={key.isPrimary}
                      >
                        Delete
                      </Menu.Item>
                    </Menu.Dropdown>
                  </Menu>
                </Group>
              </Group>
            </Card>
          ))}
        </Stack>
      )}
    </Container>
  );
}