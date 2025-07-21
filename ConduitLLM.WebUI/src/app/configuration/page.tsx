'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Card,
  TextInput,
  Alert,
  LoadingOverlay,
  Badge,
  ActionIcon,
  Grid,
  Paper,
  ThemeIcon,
  Divider,
} from '@mantine/core';
import {
  IconRefresh,
  IconInfoCircle,
  IconSettings,
  IconEdit,
  IconCheck,
  IconX,
  IconServer,
  IconClock,
  IconShield,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { notifications } from '@mantine/notifications';

interface SystemInfo {
  version: {
    appVersion: string;
    buildDate: string | null;
  };
  operatingSystem: {
    description: string;
    architecture: string;
  };
  database: {
    provider: string;
    version: string;
    connected: boolean;
    connectionString: string;
    location: string;
  };
  runtime: {
    runtimeVersion: string;
    startTime: string;
    uptime: string;
  };
  recordCounts: {
    virtualKeys: number;
    requests: number;
    settings: number;
    providers: number;
    modelMappings: number;
  };
}

interface GlobalSetting {
  id?: number;
  key: string;
  value: string;
  category?: string;
  description?: string;
  lastModified?: string;
}

export default function ConfigurationPage() {
  const [systemInfo, setSystemInfo] = useState<SystemInfo | null>(null);
  const [settings, setSettings] = useState<GlobalSetting[]>([]);
  const [systemLoading, setSystemLoading] = useState(true);
  const [settingsLoading, setSettingsLoading] = useState(true);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');

  // Fetch data on mount
  useEffect(() => {
    void fetchSystemInfo();
    void fetchSettings();
  }, []);

  const fetchSystemInfo = async () => {
    try {
      setSystemLoading(true);
      const response = await fetch('/api/settings/system-info');
      if (!response.ok) {
        throw new Error('Failed to fetch system info');
      }
      interface SystemInfoResponse {
        version?: string;
        operatingSystem?: string;
        runtime?: string;
        database?: string;
      }
      
      const data = await response.json() as SystemInfoResponse;
      
      // Validate the data structure
      if (data && typeof data === 'object' && 
          data.version && data.operatingSystem && data.runtime && data.database) {
        setSystemInfo(data as unknown as SystemInfo);
      } else {
        console.error('Invalid system info structure:', data);
        setSystemInfo(null);
      }
    } catch (err) {
      console.error('Error fetching system info:', err);
      notifications.show({
        title: 'Error',
        message: 'Failed to load system information',
        color: 'red',
      });
      setSystemInfo(null);
    } finally {
      setSystemLoading(false);
    }
  };

  const fetchSettings = async () => {
    try {
      setSettingsLoading(true);
      const response = await fetch('/api/settings');
      if (!response.ok) {
        throw new Error('Failed to fetch settings');
      }
      const data = await response.json() as unknown;
      
      // Ensure we have an array
      if (Array.isArray(data)) {
        setSettings(data);
      } else {
        console.error('Settings data is not an array:', data);
        setSettings([]);
      }
    } catch (err) {
      console.error('Error fetching settings:', err);
      notifications.show({
        title: 'Error',
        message: 'Failed to load settings',
        color: 'red',
      });
      setSettings([]); // Set empty array on error
    } finally {
      setSettingsLoading(false);
    }
  };

  const handleEdit = (setting: GlobalSetting) => {
    setEditingKey(setting.key);
    setEditValue(setting.value);
  };

  const handleSave = async (key: string) => {
    try {
      const response = await fetch(`/api/settings/${key}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ value: editValue }),
      });

      if (!response.ok) {
        throw new Error('Failed to update setting');
      }

      // Update local state
      setSettings(prevSettings =>
        prevSettings.map(s =>
          s.key === key ? { ...s, value: editValue } : s
        )
      );
      
      setEditingKey(null);
      setEditValue('');
      
      notifications.show({
        title: 'Success',
        message: 'Setting updated successfully',
        color: 'green',
      });
    } catch (error) {
      notifications.show({
        title: 'Error',
        message: `Failed to update setting: ${error instanceof Error ? error.message : 'Unknown error'}`,
        color: 'red',
      });
    }
  };

  const handleCancel = () => {
    setEditingKey(null);
    setEditValue('');
  };

  const groupedSettings = Array.isArray(settings) 
    ? settings.reduce((acc, setting) => {
        const category = setting.category ?? 'General';
        if (!acc[category]) {
          acc[category] = [];
        }
        acc[category].push(setting);
        return acc;
      }, {} as Record<string, GlobalSetting[]>)
    : {};

  const getCategoryIcon = (category: string) => {
    switch (category.toLowerCase()) {
      case 'security':
        return <IconShield size={20} />;
      case 'performance':
        return <IconClock size={20} />;
      case 'system':
        return <IconServer size={20} />;
      default:
        return <IconSettings size={20} />;
    }
  };

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Configuration</Title>
          <Text c="dimmed">Manage system-wide settings and view system information</Text>
        </div>
        <Button
          variant="light"
          leftSection={<IconRefresh size={16} />}
          onClick={() => {
            void fetchSystemInfo();
            void fetchSettings();
          }}
          loading={systemLoading || settingsLoading}
        >
          Refresh
        </Button>
      </Group>

      {/* System Information */}
      <Card withBorder>
        <Card.Section withBorder inheritPadding py="xs">
          <Group justify="space-between">
            <Group gap="xs">
              <ThemeIcon size="sm" variant="light" color="blue">
                <IconInfoCircle size={16} />
              </ThemeIcon>
              <Text fw={500}>System Information</Text>
            </Group>
          </Group>
        </Card.Section>

        <Card.Section inheritPadding py="md">
          <LoadingOverlay visible={systemLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          {systemInfo && (
            <Grid>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>App Version</Text>
                  <Text size="lg" fw={500}>{systemInfo.version.appVersion}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Runtime</Text>
                  <Text size="lg" fw={500}>{systemInfo.runtime.runtimeVersion}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Uptime</Text>
                  <Text size="lg" fw={500}>{systemInfo.runtime.uptime}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Operating System</Text>
                  <Text size="lg" fw={500}>{systemInfo.operatingSystem.description}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Architecture</Text>
                  <Text size="lg" fw={500}>{systemInfo.operatingSystem.architecture}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Database</Text>
                  <Text size="lg" fw={500}>{systemInfo.database.provider} v{systemInfo.database.version}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Virtual Keys</Text>
                  <Text size="lg" fw={500}>{systemInfo.recordCounts.virtualKeys}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Providers</Text>
                  <Text size="lg" fw={500}>{systemInfo.recordCounts.providers}</Text>
                </Paper>
              </Grid.Col>
              <Grid.Col span={{ base: 12, sm: 6, md: 4 }}>
                <Paper p="md" withBorder>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Model Mappings</Text>
                  <Text size="lg" fw={500}>{systemInfo.recordCounts.modelMappings}</Text>
                </Paper>
              </Grid.Col>
            </Grid>
          )}
        </Card.Section>
      </Card>

      {/* Settings */}
      <div style={{ position: 'relative' }}>
        <LoadingOverlay visible={settingsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
        
        {Object.keys(groupedSettings).length === 0 && !settingsLoading ? (
          <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
            <Text size="sm">
              No settings are currently available. Settings will appear here once they are configured in the system.
            </Text>
          </Alert>
        ) : (
          <Stack gap="lg">
            {Object.entries(groupedSettings).map(([category, categorySettings]) => (
              <Card key={category} withBorder>
                <Card.Section withBorder inheritPadding py="xs">
                  <Group gap="xs">
                    <ThemeIcon size="sm" variant="light">
                      {getCategoryIcon(category)}
                    </ThemeIcon>
                    <Text fw={500}>{category} Settings</Text>
                    <Badge size="sm" variant="light">
                      {categorySettings.length} {categorySettings.length === 1 ? 'setting' : 'settings'}
                    </Badge>
                  </Group>
                </Card.Section>

                <Card.Section inheritPadding py="md">
                  <Stack gap="md">
                    {categorySettings.map((setting, index) => (
                      <div key={setting.key}>
                        {index > 0 && <Divider my="xs" />}
                        <Group justify="space-between" align="flex-start">
                          <div style={{ flex: 1 }}>
                            <Text fw={500} size="sm">{setting.key}</Text>
                            {setting.description && (
                              <Text size="xs" c="dimmed" mt={2}>
                                {setting.description}
                              </Text>
                            )}
                            {editingKey === setting.key ? (
                              <Group mt="xs" gap="xs">
                                <TextInput
                                  value={editValue}
                                  onChange={(e) => setEditValue(e.currentTarget.value)}
                                  size="xs"
                                  style={{ flex: 1 }}
                                  onKeyDown={(e) => {
                                    if (e.key === 'Enter') {
                                      void handleSave(setting.key);
                                    } else if (e.key === 'Escape') {
                                      handleCancel();
                                    }
                                  }}
                                />
                                <ActionIcon
                                  color="green"
                                  variant="filled"
                                  size="sm"
                                  onClick={() => void handleSave(setting.key)}
                                  title="Save"
                                >
                                  <IconCheck size={14} />
                                </ActionIcon>
                                <ActionIcon
                                  color="red"
                                  variant="light"
                                  size="sm"
                                  onClick={handleCancel}
                                  title="Cancel"
                                >
                                  <IconX size={14} />
                                </ActionIcon>
                              </Group>
                            ) : (
                              <Group mt="xs" gap="xs" align="center">
                                <Badge variant="light" size="lg">
                                  {typeof setting.value === 'object' ? JSON.stringify(setting.value) : String(setting.value)}
                                </Badge>
                                <ActionIcon
                                  variant="subtle"
                                  size="sm"
                                  onClick={() => handleEdit(setting)}
                                  title="Edit"
                                >
                                  <IconEdit size={14} />
                                </ActionIcon>
                              </Group>
                            )}
                          </div>
                        </Group>
                      </div>
                    ))}
                  </Stack>
                </Card.Section>
              </Card>
            ))}
          </Stack>
        )}
      </div>
    </Stack>
  );
}