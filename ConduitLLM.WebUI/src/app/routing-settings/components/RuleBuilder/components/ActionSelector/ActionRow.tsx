'use client';

import { useState, useEffect } from 'react';
import {
  Card,
  Group,
  Select,
  TextInput,
  NumberInput,
  ActionIcon,
  Tooltip,
  Badge,
  Switch,
  Stack,
} from '@mantine/core';
import { IconTrash, IconGripVertical } from '@tabler/icons-react';
import { RoutingAction } from '../../../../types/routing';
import { ACTION_TYPES, getParametersForActionType } from '../../utils/ruleBuilder';
import { useProviders } from '../../../../hooks/useProviders';

interface ActionParameter {
  name: string;
  label: string;
  type: string;
  required: boolean;
  defaultValue?: unknown;
  description?: string;
  options?: Array<{ value: string; label: string }>;
  min?: number;
  max?: number;
  placeholder?: string;
}

interface ActionConfig {
  value: string;
  label: string;
  description?: string;
}

interface ActionRowProps {
  action: RoutingAction;
  index: number;
  onUpdate: (updates: Partial<RoutingAction>) => void;
  onRemove: () => void;
  canRemove: boolean;
}


export function ActionRow({ action, index, onUpdate, onRemove, canRemove }: ActionRowProps) {
  const [actionConfig, setActionConfig] = useState(ACTION_TYPES.find(a => a.value === action.type));
  const [requiredParameters, setRequiredParameters] = useState(getParametersForActionType(action.type));
  const { providerOptions, loading: providersLoading } = useProviders();

  // Helper function to safely convert values to strings for Select components
  const safeToString = (value: unknown): string | null => {
    if (value === null || value === undefined) return null;
    if (typeof value === 'string') return value;
    if (typeof value === 'number' || typeof value === 'boolean') return String(value);
    return null; // For objects and other types, return null instead of stringifying
  };

  useEffect(() => {
    const config = ACTION_TYPES.find(a => a.value === action.type);
    setActionConfig(config);
    setRequiredParameters(getParametersForActionType(action.type));
  }, [action.type]);

  const handleTypeChange = (value: string | null) => {
    if (!value) return;
    
    const newParameters = getParametersForActionType(value);
    const resetParameters: Record<string, unknown> = {};
    
    // Initialize parameters with default values
    newParameters.forEach((param: ActionParameter) => {
      if (param.required) {
        resetParameters[param.name] = param.defaultValue ?? '';
      }
    });
    
    onUpdate({
      type: value as RoutingAction['type'],
      target: value === 'route' ? action.target : undefined,
      parameters: resetParameters,
    });
  };

  const handleParameterChange = (paramName: string, value: unknown) => {
    const updatedParameters = { ...action.parameters, [paramName]: value };
    onUpdate({ parameters: updatedParameters });
  };

  const renderParameterInput = (param: ActionParameter) => {
    const currentValue = action.parameters?.[param.name];

    switch (param.type) {
      case 'number':
        return (
          <NumberInput
            key={param.name}
            label={param.label}
            placeholder={(param).placeholder}
            value={typeof currentValue === 'number' ? currentValue : undefined}
            onChange={(value) => handleParameterChange(param.name, value)}
            min={(param).min}
            max={(param).max}
            required={param.required}
          />
        );

      case 'boolean':
        return (
          <Switch
            key={param.name}
            label={param.label}
            description={(param).description}
            checked={Boolean(currentValue)}
            onChange={(e) => handleParameterChange(param.name, e.target.checked)}
          />
        );

      case 'select':
        return (
          <Select
            key={param.name}
            label={param.label}
            placeholder={(param).placeholder}
            data={(param).options ?? []}
            value={safeToString(currentValue)}
            onChange={(value) => handleParameterChange(param.name, value)}
            searchable
            required={param.required}
          />
        );

      case 'provider':
        return (
          <Select
            key={param.name}
            label={param.label}
            placeholder="Select provider"
            data={providerOptions}
            disabled={providersLoading}
            value={safeToString(currentValue)}
            onChange={(value) => handleParameterChange(param.name, value)}
            searchable
            required={param.required}
          />
        );

      default:
        return (
          <TextInput
            key={param.name}
            label={param.label}
            placeholder={(param).placeholder}
            value={typeof currentValue === 'string' || typeof currentValue === 'number' ? String(currentValue) : ''}
            onChange={(e) => handleParameterChange(param.name, e.target.value)}
            required={param.required}
          />
        );
    }
  };

  return (
    <Card withBorder p="md" bg={index % 2 === 0 ? 'white' : 'gray.0'}>
      <Stack gap="md">
        <Group align="flex-start" gap="md">
          {/* Drag Handle */}
          <ActionIcon variant="subtle" color="gray" style={{ cursor: 'grab' }}>
            <IconGripVertical size={16} />
          </ActionIcon>

          {/* Action Index */}
          <Badge variant="light" size="sm" color="green">
            {index + 1}
          </Badge>

          {/* Action Type Selector */}
          <div style={{ flex: 1, minWidth: 200 }}>
            <Select
              label="Action Type"
              data={ACTION_TYPES}
              value={String(action.type)}
              onChange={handleTypeChange}
              searchable
            />
          </div>

          {/* Target Input (for route actions) */}
          {action.type === 'route' && (
            <div style={{ flex: 1, minWidth: 150 }}>
              <Select
                label="Target Provider"
                placeholder="Select provider"
                data={providerOptions}
            disabled={providersLoading}
                value={safeToString(action.target) ?? ''}
                onChange={(value) => onUpdate({ target: value ?? '' })}
                searchable
              />
            </div>
          )}

          {/* Remove Button */}
          <div style={{ paddingTop: 25 }}>
            <Tooltip
              label={canRemove ? 'Remove action' : 'At least one action is required'}
              position="left"
            >
              <ActionIcon
                color="red"
                variant="subtle"
                onClick={onRemove}
                disabled={!canRemove}
              >
                <IconTrash size={16} />
              </ActionIcon>
            </Tooltip>
          </div>
        </Group>

        {/* Action Parameters */}
        {requiredParameters.length > 0 && (
          <Card withBorder p="sm" bg="blue.0">
            <Stack gap="sm">
              {requiredParameters.map(param => renderParameterInput(param))}
            </Stack>
          </Card>
        )}

        {/* Action Description */}
        {actionConfig && (
          <Card withBorder p="xs" bg="gray.1">
            <Group gap="xs">
              <Badge size="xs" variant="dot" color="blue">
                Info
              </Badge>
              <span style={{ fontSize: 12, color: '#666' }}>
                {(actionConfig as ActionConfig)?.description ?? 'No description available'}
              </span>
            </Group>
          </Card>
        )}
      </Stack>
    </Card>
  );
}