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

  useEffect(() => {
    const config = ACTION_TYPES.find(a => a.value === action.type);
    setActionConfig(config);
    setRequiredParameters(getParametersForActionType(action.type));
  }, [action.type]);

  const handleTypeChange = (value: string | null) => {
    if (!value) return;
    
    const newParameters = getParametersForActionType(value);
    const resetParameters: Record<string, any> = {};
    
    // Initialize parameters with default values
    newParameters.forEach(param => {
      if (param.required) {
        resetParameters[param.name] = (param as any).defaultValue || '';
      }
    });
    
    onUpdate({
      type: value as any,
      target: value === 'route' ? action.target : undefined,
      parameters: resetParameters,
    });
  };

  const handleParameterChange = (paramName: string, value: any) => {
    const updatedParameters = { ...action.parameters, [paramName]: value };
    onUpdate({ parameters: updatedParameters });
  };

  const renderParameterInput = (param: any) => {
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
            data={(param).options || []}
            value={currentValue}
            onChange={(value) => handleParameterChange(param.name, value)}
            searchable={(param).searchable}
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
            value={currentValue}
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
            value={currentValue || ''}
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
              value={action.type}
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
                value={action.target || ''}
                onChange={(value) => onUpdate({ target: value || '' })}
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
                {(actionConfig as any)?.description}
              </span>
            </Group>
          </Card>
        )}
      </Stack>
    </Card>
  );
}