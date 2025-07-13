'use client';

import {
  Group,
  Button,
  Menu,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import {
  IconToggleLeft,
  IconToggleRight,
  IconRestore,
  IconDots,
  IconDownload,
  IconUpload,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';

interface ProviderDisplay {
  providerId: string;
  providerName: string;
  priority: number;
  weight?: number;
  isEnabled: boolean;
  statistics: {
    usagePercentage: number;
    successRate: number;
    avgResponseTime: number;
  };
  type: 'primary' | 'backup' | 'special';
}

interface BulkActionsProps {
  providers: ProviderDisplay[];
  onAction: (action: 'enable-all' | 'disable-all' | 'reset') => void;
  disabled: boolean;
}

export function BulkActions({ providers, onAction, disabled }: BulkActionsProps) {
  const enabledCount = providers.filter(p => p.isEnabled).length;
  const disabledCount = providers.length - enabledCount;

  const handleExportConfiguration = () => {
    const config = {
      timestamp: new Date().toISOString(),
      providers: providers.map(p => ({
        providerId: p.providerId,
        providerName: p.providerName,
        priority: p.priority,
        weight: p.weight,
        isEnabled: p.isEnabled,
        type: p.type,
      }))
    };

    const blob = new Blob([JSON.stringify(config, null, 2)], { 
      type: 'application/json' 
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `provider-priorities-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    notifications.show({
      title: 'Configuration Exported',
      message: 'Provider configuration has been downloaded',
      color: 'green',
    });
  };

  const handleImportConfiguration = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        const reader = new FileReader();
        reader.onload = (e) => {
          try {
            const config = JSON.parse(e.target?.result as string);
            // Here you would validate and apply the imported configuration
            notifications.show({
              title: 'Import Feature',
              message: 'Import functionality would be implemented here',
              color: 'blue',
            });
          } catch (err) {
            notifications.show({
              title: 'Import Error',
              message: 'Invalid configuration file format',
              color: 'red',
            });
          }
        };
        reader.readAsText(file);
      }
    };
    input.click();
  };

  return (
    <Group gap="xs">
      {/* Enable All */}
      <Tooltip 
        label={`Enable all providers (${disabledCount} disabled)`}
        disabled={disabledCount === 0}
      >
        <Button
          leftSection={<IconToggleRight size={16} />}
          variant="light"
          color="green"
          size="sm"
          onClick={() => onAction('enable-all')}
          disabled={disabled || disabledCount === 0}
        >
          Enable All
        </Button>
      </Tooltip>

      {/* Disable All */}
      <Tooltip 
        label={enabledCount <= 1 ? 'Cannot disable all providers' : `Disable all providers (${enabledCount} enabled)`}
      >
        <Button
          leftSection={<IconToggleLeft size={16} />}
          variant="light"
          color="orange"
          size="sm"
          onClick={() => onAction('disable-all')}
          disabled={disabled || enabledCount <= 1}
        >
          Disable All
        </Button>
      </Tooltip>

      {/* Reset to Default */}
      <Tooltip label="Reset to original configuration">
        <Button
          leftSection={<IconRestore size={16} />}
          variant="light"
          color="gray"
          size="sm"
          onClick={() => onAction('reset')}
          disabled={disabled}
        >
          Reset to Default
        </Button>
      </Tooltip>

      {/* More Actions Menu */}
      <Menu shadow="md" width={200} position="bottom-end">
        <Menu.Target>
          <ActionIcon variant="light" size="lg" disabled={disabled}>
            <IconDots size={16} />
          </ActionIcon>
        </Menu.Target>

        <Menu.Dropdown>
          <Menu.Label>Configuration</Menu.Label>
          <Menu.Item
            leftSection={<IconDownload size={14} />}
            onClick={handleExportConfiguration}
          >
            Export Configuration
          </Menu.Item>
          <Menu.Item
            leftSection={<IconUpload size={14} />}
            onClick={handleImportConfiguration}
          >
            Import Configuration
          </Menu.Item>
          
          <Menu.Divider />
          
          <Menu.Label>Statistics</Menu.Label>
          <Menu.Item disabled>
            {enabledCount} / {providers.length} enabled
          </Menu.Item>
          <Menu.Item disabled>
            Avg Success Rate: {providers.length > 0 ? 
              (providers.reduce((sum, p) => sum + p.statistics.successRate, 0) / providers.length).toFixed(1) 
              : 0}%
          </Menu.Item>
        </Menu.Dropdown>
      </Menu>
    </Group>
  );
}