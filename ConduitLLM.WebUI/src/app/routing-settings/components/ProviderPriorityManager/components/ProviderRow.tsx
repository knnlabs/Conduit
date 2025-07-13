'use client';

import { useState, useEffect } from 'react';
import {
  Table,
  NumberInput,
  Switch,
  Badge,
  Text,
  Progress,
  Group,
  Tooltip,
  ActionIcon,
  Alert,
} from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
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

interface ProviderRowProps {
  provider: ProviderDisplay;
  index: number;
  onUpdate: (index: number, updates: Partial<ProviderDisplay>) => void;
  isLoading: boolean;
  allProviders: ProviderDisplay[];
}

export function ProviderRow({ 
  provider, 
  index, 
  onUpdate, 
  isLoading, 
  allProviders 
}: ProviderRowProps) {
  const [priorityError, setPriorityError] = useState<string | null>(null);

  const getProviderTypeColor = (type: string) => {
    switch (type) {
      case 'primary': return 'blue';
      case 'backup': return 'orange';
      case 'special': return 'green';
      default: return 'gray';
    }
  };

  const getSuccessRateColor = (rate: number) => {
    if (rate >= 95) return 'green';
    if (rate >= 85) return 'yellow';
    return 'red';
  };

  const getResponseTimeColor = (time: number) => {
    if (time <= 300) return 'green';
    if (time <= 500) return 'yellow';
    return 'red';
  };

  const validatePriority = (newPriority: number) => {
    // Check for duplicate priorities
    const duplicates = allProviders.filter(p => 
      p.providerId !== provider.providerId && p.priority === newPriority
    );
    
    if (duplicates.length > 0) {
      setPriorityError(`Priority ${newPriority} is already used by ${duplicates[0].providerName}`);
      return false;
    }
    
    // Check priority range
    if (newPriority < 1 || newPriority > allProviders.length) {
      setPriorityError(`Priority must be between 1 and ${allProviders.length}`);
      return false;
    }
    
    setPriorityError(null);
    return true;
  };

  const handlePriorityChange = (value: number | string) => {
    const newPriority = typeof value === 'string' ? parseInt(value, 10) : value;
    
    if (isNaN(newPriority)) {
      setPriorityError('Priority must be a number');
      return;
    }

    if (validatePriority(newPriority)) {
      onUpdate(index, { priority: newPriority });
    }
  };

  const handleEnabledChange = (checked: boolean) => {
    // Prevent disabling if this is the last enabled provider
    if (!checked) {
      const enabledCount = allProviders.filter(p => p.isEnabled).length;
      if (enabledCount <= 1) {
        notifications.show({
          title: 'Action Prevented',
          message: 'At least one provider must remain enabled',
          color: 'orange',
        });
        return;
      }
    }

    // Warn if disabling a high-usage provider
    if (!checked && provider.statistics.usagePercentage > 25) {
      notifications.show({
        title: 'High Usage Provider',
        message: `Warning: ${provider.providerName} handles ${provider.statistics.usagePercentage}% of traffic`,
        color: 'yellow',
      });
    }

    onUpdate(index, { isEnabled: checked });
  };

  const handleWeightChange = (value: number | string) => {
    const newWeight = typeof value === 'string' ? parseInt(value, 10) : value;
    if (!isNaN(newWeight) && newWeight >= 1 && newWeight <= 100) {
      onUpdate(index, { weight: newWeight });
    }
  };

  return (
    <Table.Tr>
      {/* Priority Input */}
      <Table.Td>
        <div style={{ position: 'relative' }}>
          <NumberInput
            value={provider.priority}
            onChange={handlePriorityChange}
            min={1}
            max={allProviders.length}
            size="sm"
            w={60}
            error={!!priorityError}
            disabled={isLoading}
          />
          {priorityError && (
            <Tooltip label={priorityError} position="bottom" withArrow opened>
              <ActionIcon 
                size="xs" 
                color="red" 
                variant="subtle"
                style={{ position: 'absolute', top: 2, right: 2 }}
              >
                <IconAlertTriangle size={12} />
              </ActionIcon>
            </Tooltip>
          )}
        </div>
      </Table.Td>

      {/* Provider Name and ID */}
      <Table.Td>
        <div>
          <Text fw={500} size="sm">{provider.providerName}</Text>
          <Text size="xs" c="dimmed">{provider.providerId}</Text>
        </div>
      </Table.Td>

      {/* Provider Type */}
      <Table.Td>
        <Badge 
          variant="light" 
          color={getProviderTypeColor(provider.type)}
          size="sm"
        >
          {provider.type}
        </Badge>
      </Table.Td>

      {/* Status */}
      <Table.Td>
        <Switch
          checked={provider.isEnabled}
          onChange={(e) => handleEnabledChange(e.target.checked)}
          size="sm"
          disabled={isLoading}
          onLabel="ON"
          offLabel="OFF"
        />
      </Table.Td>

      {/* Usage Percentage */}
      <Table.Td>
        <div style={{ width: '100%' }}>
          <Group justify="space-between" mb={2}>
            <Text size="xs" fw={500}>
              {provider.statistics.usagePercentage.toFixed(1)}%
            </Text>
          </Group>
          <Progress
            value={provider.statistics.usagePercentage}
            size="xs"
            color={provider.isEnabled ? 'blue' : 'gray'}
          />
        </div>
      </Table.Td>

      {/* Success Rate */}
      <Table.Td>
        <div style={{ width: '100%' }}>
          <Group justify="space-between" mb={2}>
            <Text size="xs" fw={500}>
              {provider.statistics.successRate.toFixed(1)}%
            </Text>
          </Group>
          <Progress
            value={provider.statistics.successRate}
            size="xs"
            color={getSuccessRateColor(provider.statistics.successRate)}
          />
        </div>
      </Table.Td>

      {/* Average Response Time */}
      <Table.Td>
        <Badge 
          variant="light" 
          color={getResponseTimeColor(provider.statistics.avgResponseTime)}
          size="sm"
        >
          {provider.statistics.avgResponseTime.toFixed(0)}ms
        </Badge>
      </Table.Td>

      {/* Weight */}
      <Table.Td>
        {provider.weight !== undefined ? (
          <NumberInput
            value={provider.weight}
            onChange={handleWeightChange}
            min={1}
            max={100}
            size="sm"
            w={60}
            disabled={isLoading}
          />
        ) : (
          <Text size="xs" c="dimmed">N/A</Text>
        )}
      </Table.Td>

      {/* Actions */}
      <Table.Td>
        <Group gap="xs">
          {provider.statistics.usagePercentage > 50 && (
            <Tooltip label="High usage provider" withArrow>
              <Badge color="orange" size="xs" variant="filled">
                High
              </Badge>
            </Tooltip>
          )}
          {provider.statistics.successRate < 90 && (
            <Tooltip label="Low success rate" withArrow>
              <Badge color="red" size="xs" variant="filled">
                Alert
              </Badge>
            </Tooltip>
          )}
        </Group>
      </Table.Td>
    </Table.Tr>
  );
}