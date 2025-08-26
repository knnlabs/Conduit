'use client';

import { useMemo, useCallback } from 'react';
import { Stack, Paper, Title, Text, Group, Badge, Collapse, ActionIcon, Tooltip } from '@mantine/core';
import { IconChevronDown, IconChevronUp, IconRefresh, IconSettings } from '@tabler/icons-react';
import { useDisclosure } from '@mantine/hooks';
import { ParameterRenderer } from './ParameterRenderer';
import type { DynamicParameter, ParameterValues, ParameterContext } from './types/parameters';

interface DynamicParametersProps {
  context: ParameterContext;
  parameters: Record<string, DynamicParameter> | string;
  values: ParameterValues;
  onChange: (values: ParameterValues) => void;
  onReset?: () => void;
  title?: string;
  description?: string;
  collapsible?: boolean;
  defaultExpanded?: boolean;
  showCount?: boolean;
  className?: string;
}

export function DynamicParameters({
  context,
  parameters,
  values,
  onChange,
  onReset,
  title,
  description,
  collapsible = true,
  defaultExpanded = false,
  showCount = true,
  className,
}: DynamicParametersProps) {
  const [expanded, { toggle }] = useDisclosure(defaultExpanded);

  // Parse parameters if they're provided as a JSON string
  const parsedParameters = useMemo(() => {
    if (typeof parameters === 'string') {
      try {
        return JSON.parse(parameters) as Record<string, DynamicParameter>;
      } catch (error) {
        console.error('Failed to parse parameters:', error);
        return {};
      }
    }
    return parameters;
  }, [parameters]);

  // Get visible parameters based on dependencies
  const visibleParameters = useMemo(() => {
    return Object.entries(parsedParameters).filter(([, param]) => {
      if (param.visible === false) return false;
      
      if (param.dependsOn) {
        const dependencyValue = values[param.dependsOn.parameter];
        return dependencyValue === param.dependsOn.value;
      }
      
      return true;
    });
  }, [parsedParameters, values]);

  // Handle parameter value change
  const handleParameterChange = useCallback((key: string, value: unknown) => {
    const newValues = {
      ...values,
      [key]: value,
    };
    onChange(newValues);
  }, [values, onChange]);

  // Handle reset to defaults
  const handleReset = useCallback(() => {
    const defaultValues: ParameterValues = {};
    Object.entries(parsedParameters).forEach(([key, param]) => {
      if (param.default !== undefined) {
        defaultValues[key] = param.default as unknown;
      }
    });
    onChange(defaultValues);
    onReset?.();
  }, [parsedParameters, onChange, onReset]);

  // Count of active (non-default) parameters
  const activeCount = useMemo(() => {
    return Object.entries(values).filter(([key, value]) => {
      const param = parsedParameters[key];
      return param && value !== param.default;
    }).length;
  }, [values, parsedParameters]);

  if (visibleParameters.length === 0) {
    return null;
  }

  const header = (
    <Group justify="space-between" mb={collapsible && !expanded ? 0 : 'md'}>
      <Group>
        {collapsible && (
          <ActionIcon 
            variant="subtle" 
            onClick={toggle}
            size="sm"
          >
            {expanded ? <IconChevronUp size={16} /> : <IconChevronDown size={16} />}
          </ActionIcon>
        )}
        <Group gap="xs">
          <IconSettings size={18} />
          <Title order={5}>{title ?? `${context.charAt(0).toUpperCase() + context.slice(1)} Parameters`}</Title>
          {showCount && activeCount > 0 && (
            <Badge size="sm" variant="filled">
              {activeCount} active
            </Badge>
          )}
        </Group>
      </Group>
      <Tooltip label="Reset to defaults">
        <ActionIcon 
          variant="subtle" 
          onClick={handleReset}
          size="sm"
        >
          <IconRefresh size={16} />
        </ActionIcon>
      </Tooltip>
    </Group>
  );

  const content = (
    <Stack gap="lg" mt={expanded || !collapsible ? 'md' : 0}>
      {description && (
        <Text size="sm" c="dimmed" mb="sm">{description}</Text>
      )}
      
      {visibleParameters.map(([key, parameter]) => (
        <ParameterRenderer
          key={key}
          parameter={parameter}
          value={values[key]}
          onChange={(value) => handleParameterChange(key, value)}
          context={context}
        />
      ))}
    </Stack>
  );

  return (
    <Paper p="md" className={className} withBorder>
      {header}
      {collapsible ? (
        <Collapse in={expanded}>
          {content}
        </Collapse>
      ) : (
        content
      )}
    </Paper>
  );
}