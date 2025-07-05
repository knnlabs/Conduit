'use client';

import {
  Stack,
  Title,
  Text,
  Group,
  Button,
  Switch,
  NumberInput,
  TextInput,
  Select,
  Textarea,
  Alert,
  LoadingOverlay,
} from '@mantine/core';
import {
  IconDeviceFloppy,
  IconRefresh,
  IconInfoCircle,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useForm } from '@mantine/form';
import { SettingsCard, SettingRow } from '@/components/configuration/SettingsCard';
import { useSystemInfo, useSystemSettings, useUpdateSystemSettings } from '@/hooks/api/useAdminApi';
import { notifications } from '@mantine/notifications';

interface SystemSettings {
  // General Settings
  systemName: string;
  description: string;
  enableLogging: boolean;
  logLevel: string;
  
  // Performance Settings
  maxConcurrentRequests: number;
  requestTimeoutSeconds: number;
  cacheTimeoutMinutes: number;
  
  // Rate Limiting
  enableRateLimiting: boolean;
  maxRequestsPerMinute: number;
  rateLimitWindowSeconds: number;
  
  // Security Settings
  enableIpFiltering: boolean;
  enableRequestValidation: boolean;
  maxFailedAttempts: number;
  
  // Monitoring
  enablePerformanceTracking: boolean;
  enableHealthChecks: boolean;
  healthCheckIntervalMinutes: number;
}

export default function ConfigurationPage() {
  const { data: systemInfo, isLoading: systemLoading } = useSystemInfo();
  const { data: systemSettings, isLoading: settingsLoading } = useSystemSettings();
  const updateSystemSettings = useUpdateSystemSettings();
  const [_isLoading, _setIsLoading] = useState(false);
  const [editingSection, setEditingSection] = useState<string | null>(null);

  const form = useForm<SystemSettings>({
    initialValues: {
      systemName: 'Conduit LLM Platform',
      description: 'Unified LLM API Gateway and Management Platform',
      enableLogging: true,
      logLevel: 'Information',
      maxConcurrentRequests: 100,
      requestTimeoutSeconds: 30,
      cacheTimeoutMinutes: 30,
      enableRateLimiting: false,
      maxRequestsPerMinute: 1000,
      rateLimitWindowSeconds: 60,
      enableIpFiltering: false,
      enableRequestValidation: true,
      maxFailedAttempts: 5,
      enablePerformanceTracking: true,
      enableHealthChecks: true,
      healthCheckIntervalMinutes: 5,
    },
  });

  // Update form when settings are loaded
  useEffect(() => {
    if (systemSettings) {
      form.setValues(systemSettings);
    }
  }, [systemSettings, form]);

  const handleSaveSection = async (_section: string) => {
    try {
      // Convert form values to Record<string, string>
      const settings: Record<string, string> = {};
      Object.entries(form.values).forEach(([key, value]) => {
        settings[key] = String(value);
      });
      await updateSystemSettings.mutateAsync(settings);
      setEditingSection(null);
    } catch (_error) {
      // Error notification is handled by the mutation hook
    }
  };

  const handleResetDefaults = () => {
    form.reset();
    notifications.show({
      title: 'Settings Reset',
      message: 'All settings have been reset to default values',
      color: 'blue',
    });
  };

  const logLevelOptions = [
    { value: 'Trace', label: 'Trace' },
    { value: 'Debug', label: 'Debug' },
    { value: 'Information', label: 'Information' },
    { value: 'Warning', label: 'Warning' },
    { value: 'Error', label: 'Error' },
    { value: 'Critical', label: 'Critical' },
  ];

  return (
    <Stack gap="xl">
      <Group justify="space-between">
        <div>
          <Title order={1}>System Configuration</Title>
          <Text c="dimmed">Configure system-wide settings and preferences</Text>
        </div>

        <Group>
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            onClick={handleResetDefaults}
          >
            Reset to Defaults
          </Button>
          <Button
            leftSection={<IconDeviceFloppy size={16} />}
            disabled={!form.isDirty()}
            loading={updateSystemSettings.isPending}
            onClick={() => handleSaveSection('All')}
          >
            Save All Changes
          </Button>
        </Group>
      </Group>

      {systemInfo && (
        <Alert icon={<IconInfoCircle size={16} />} color="blue" variant="light">
          <Text size="sm">
            <strong>System Version:</strong> {systemInfo.version || 'Unknown'} | 
            <strong> Environment:</strong> {systemInfo.environment || 'Production'} | 
            <strong> Uptime:</strong> {systemInfo.uptime || 'Unknown'}
          </Text>
        </Alert>
      )}

      <div style={{ position: 'relative' }}>
        <LoadingOverlay visible={systemLoading || settingsLoading} overlayProps={{ radius: 'sm', blur: 2 }} />

        <Stack gap="lg">
          {/* General Settings */}
          <SettingsCard
            title="General Settings"
            description="Basic system configuration and identification"
            category="General"
            isEditing={editingSection === 'general'}
            onToggleEdit={() => setEditingSection(editingSection === 'general' ? null : 'general')}
            onSave={() => handleSaveSection('General')}
            isDirty={form.isDirty()}
          >
            <Stack gap="md">
              <SettingRow
                label="System Name"
                description="Display name for this Conduit instance"
                required
              >
                <TextInput
                  {...form.getInputProps('systemName')}
                  disabled={editingSection !== 'general'}
                />
              </SettingRow>

              <SettingRow
                label="Description"
                description="Optional description of this system"
              >
                <Textarea
                  rows={2}
                  {...form.getInputProps('description')}
                  disabled={editingSection !== 'general'}
                />
              </SettingRow>

              <SettingRow
                label="Enable Logging"
                description="Enable system-wide logging"
              >
                <Switch
                  {...form.getInputProps('enableLogging', { type: 'checkbox' })}
                  disabled={editingSection !== 'general'}
                />
              </SettingRow>

              <SettingRow
                label="Log Level"
                description="Minimum log level to record"
              >
                <Select
                  data={logLevelOptions}
                  {...form.getInputProps('logLevel')}
                  disabled={editingSection !== 'general' || !form.values.enableLogging}
                />
              </SettingRow>
            </Stack>
          </SettingsCard>

          {/* Performance Settings */}
          <SettingsCard
            title="Performance Settings"
            description="Configure system performance and resource limits"
            category="Performance"
            isEditing={editingSection === 'performance'}
            onToggleEdit={() => setEditingSection(editingSection === 'performance' ? null : 'performance')}
            onSave={() => handleSaveSection('Performance')}
            isDirty={form.isDirty()}
          >
            <Stack gap="md">
              <SettingRow
                label="Max Concurrent Requests"
                description="Maximum number of simultaneous requests"
                required
              >
                <NumberInput
                  min={1}
                  max={1000}
                  {...form.getInputProps('maxConcurrentRequests')}
                  disabled={editingSection !== 'performance'}
                />
              </SettingRow>

              <SettingRow
                label="Request Timeout (seconds)"
                description="Default timeout for API requests"
                required
              >
                <NumberInput
                  min={5}
                  max={300}
                  {...form.getInputProps('requestTimeoutSeconds')}
                  disabled={editingSection !== 'performance'}
                />
              </SettingRow>

              <SettingRow
                label="Cache Timeout (minutes)"
                description="How long to cache responses"
                required
              >
                <NumberInput
                  min={1}
                  max={1440}
                  {...form.getInputProps('cacheTimeoutMinutes')}
                  disabled={editingSection !== 'performance'}
                />
              </SettingRow>
            </Stack>
          </SettingsCard>

          {/* Rate Limiting Settings */}
          <SettingsCard
            title="Rate Limiting"
            description="Configure request rate limiting and throttling"
            category="Security"
            isEditing={editingSection === 'rateLimit'}
            onToggleEdit={() => setEditingSection(editingSection === 'rateLimit' ? null : 'rateLimit')}
            onSave={() => handleSaveSection('Rate Limiting')}
            isDirty={form.isDirty()}
          >
            <Stack gap="md">
              <SettingRow
                label="Enable Rate Limiting"
                description="Enable request rate limiting"
              >
                <Switch
                  {...form.getInputProps('enableRateLimiting', { type: 'checkbox' })}
                  disabled={editingSection !== 'rateLimit'}
                />
              </SettingRow>

              <SettingRow
                label="Max Requests per Minute"
                description="Maximum requests allowed per minute per client"
              >
                <NumberInput
                  min={1}
                  max={10000}
                  {...form.getInputProps('maxRequestsPerMinute')}
                  disabled={editingSection !== 'rateLimit' || !form.values.enableRateLimiting}
                />
              </SettingRow>

              <SettingRow
                label="Rate Limit Window (seconds)"
                description="Time window for rate limit calculations"
              >
                <NumberInput
                  min={1}
                  max={3600}
                  {...form.getInputProps('rateLimitWindowSeconds')}
                  disabled={editingSection !== 'rateLimit' || !form.values.enableRateLimiting}
                />
              </SettingRow>
            </Stack>
          </SettingsCard>

          {/* Security Settings */}
          <SettingsCard
            title="Security Settings"
            description="Configure security and access control features"
            category="Security"
            isEditing={editingSection === 'security'}
            onToggleEdit={() => setEditingSection(editingSection === 'security' ? null : 'security')}
            onSave={() => handleSaveSection('Security')}
            isDirty={form.isDirty()}
          >
            <Stack gap="md">
              <SettingRow
                label="Enable IP Filtering"
                description="Enable IP-based access control"
              >
                <Switch
                  {...form.getInputProps('enableIpFiltering', { type: 'checkbox' })}
                  disabled={editingSection !== 'security'}
                />
              </SettingRow>

              <SettingRow
                label="Enable Request Validation"
                description="Validate incoming requests"
              >
                <Switch
                  {...form.getInputProps('enableRequestValidation', { type: 'checkbox' })}
                  disabled={editingSection !== 'security'}
                />
              </SettingRow>

              <SettingRow
                label="Max Failed Attempts"
                description="Maximum failed login attempts before blocking"
              >
                <NumberInput
                  min={1}
                  max={100}
                  {...form.getInputProps('maxFailedAttempts')}
                  disabled={editingSection !== 'security'}
                />
              </SettingRow>
            </Stack>
          </SettingsCard>

          {/* Monitoring Settings */}
          <SettingsCard
            title="Monitoring & Health Checks"
            description="Configure system monitoring and health check settings"
            category="Monitoring"
            isEditing={editingSection === 'monitoring'}
            onToggleEdit={() => setEditingSection(editingSection === 'monitoring' ? null : 'monitoring')}
            onSave={() => handleSaveSection('Monitoring')}
            isDirty={form.isDirty()}
          >
            <Stack gap="md">
              <SettingRow
                label="Enable Performance Tracking"
                description="Track and log performance metrics"
              >
                <Switch
                  {...form.getInputProps('enablePerformanceTracking', { type: 'checkbox' })}
                  disabled={editingSection !== 'monitoring'}
                />
              </SettingRow>

              <SettingRow
                label="Enable Health Checks"
                description="Perform automated health checks"
              >
                <Switch
                  {...form.getInputProps('enableHealthChecks', { type: 'checkbox' })}
                  disabled={editingSection !== 'monitoring'}
                />
              </SettingRow>

              <SettingRow
                label="Health Check Interval (minutes)"
                description="How often to perform health checks"
              >
                <NumberInput
                  min={1}
                  max={60}
                  {...form.getInputProps('healthCheckIntervalMinutes')}
                  disabled={editingSection !== 'monitoring' || !form.values.enableHealthChecks}
                />
              </SettingRow>
            </Stack>
          </SettingsCard>
        </Stack>
      </div>
    </Stack>
  );
}