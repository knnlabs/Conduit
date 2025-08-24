'use client';

import { useState, useMemo } from 'react';
import { Checkbox, Paper, Text, Stack, Alert, Divider, ScrollArea, Box } from '@mantine/core';
import { IconAlertCircle } from '@tabler/icons-react';
import { ParameterRenderer } from './ParameterRenderer';
import type { DynamicParameter, ParameterValues, ParameterContext } from './types/parameters';

interface ParameterPreviewProps {
  parametersJson: string;
  context?: ParameterContext;
  label?: string;
  maxHeight?: number;
}

export function ParameterPreview({ 
  parametersJson, 
  context = 'chat',
  label = 'Preview UI Components',
  maxHeight = 400
}: ParameterPreviewProps) {
  const [showPreview, setShowPreview] = useState(false);
  const [values, setValues] = useState<ParameterValues>({});

  const { parameters, error } = useMemo(() => {
    if (!parametersJson || parametersJson.trim() === '{}' || !showPreview) {
      return { parameters: null, error: null };
    }

    try {
      const parsed = JSON.parse(parametersJson) as Record<string, unknown>;
      const params: Record<string, DynamicParameter> = {};
      
      // Initialize values for each parameter
      const initialValues: ParameterValues = {};
      
      for (const [key, value] of Object.entries(parsed)) {
        if (typeof value === 'object' && value !== null && 'type' in value) {
          const param = value as unknown as DynamicParameter;
          params[key] = param;
          
          // Set initial value from default or appropriate type default
          if (param.default !== undefined) {
            initialValues[key] = param.default;
          } else {
            // Set type-appropriate defaults
            switch (param.type) {
              case 'slider': {
                const sliderParam = param as { min?: number };
                initialValues[key] = sliderParam.min ?? 0;
                break;
              }
              case 'number':
                initialValues[key] = 0;
                break;
              case 'toggle':
                initialValues[key] = false;
                break;
              case 'text':
              case 'textarea':
              case 'color':
                initialValues[key] = '';
                break;
              case 'select':
              case 'resolution': {
                const selectParam = param as { options?: Array<{ value: string | number }> };
                const opts = selectParam.options;
                initialValues[key] = Array.isArray(opts) && opts.length > 0 
                  ? opts[0].value 
                  : '';
                break;
              }
            }
          }
        }
      }
      
      // Only set initial values once when preview is first shown
      if (Object.keys(values).length === 0 && Object.keys(initialValues).length > 0) {
        setValues(initialValues);
      }
      
      return { parameters: params, error: null };
    } catch (e) {
      return { 
        parameters: null, 
        error: e instanceof Error ? e.message : 'Invalid JSON format' 
      };
    }
  }, [parametersJson, showPreview, values]);

  const handleValueChange = (key: string) => (value: unknown) => {
    setValues(prev => ({ ...prev, [key]: value }));
  };

  const hasParameters = useMemo(() => {
    try {
      const parsed = JSON.parse(parametersJson || '{}') as Record<string, unknown>;
      return Object.keys(parsed).length > 0;
    } catch {
      return false;
    }
  }, [parametersJson]);

  if (!hasParameters) {
    return null;
  }

  return (
    <Stack gap="sm">
      <Checkbox
        label={label}
        checked={showPreview}
        onChange={(event) => setShowPreview(event.currentTarget.checked)}
      />
      
      {showPreview && (
        <Paper p="md" withBorder>
          {(() => {
            if (error) {
              return (
                <Alert icon={<IconAlertCircle size={16} />} color="red" variant="light">
                  <Text size="sm">Failed to parse parameters: {error}</Text>
                </Alert>
              );
            }
            
            if (parameters && Object.keys(parameters).length > 0) {
              return (
                <Stack gap="md">
              <Text size="sm" fw={500} c="dimmed">
                Interactive Preview - These controls demonstrate how parameters will appear to users
              </Text>
              <Divider />
              <ScrollArea h={maxHeight} offsetScrollbars>
                <Stack gap="lg">
                  {Object.entries(parameters).map(([key, param]) => (
                    <Box key={key}>
                      <Stack gap="xs">
                        <Text size="xs" c="dimmed" tt="uppercase" fw={600}>
                          {key}
                        </Text>
                        <ParameterRenderer
                          parameter={param}
                          value={values[key]}
                          onChange={handleValueChange(key)}
                          context={context}
                          disabled={false}
                        />
                      </Stack>
                    </Box>
                  ))}
                </Stack>
              </ScrollArea>
              <Divider />
              <Box>
                <Text size="xs" c="dimmed" mb="xs">Current Values:</Text>
                <Paper p="xs" withBorder bg="gray.0">
                  <Text size="xs" style={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>
                    {JSON.stringify(values, null, 2)}
                  </Text>
                </Paper>
              </Box>
                </Stack>
              );
            }
            
            return (
              <Text size="sm" c="dimmed">
                No parameters to preview
              </Text>
            );
          })()}
        </Paper>
      )}
    </Stack>
  );
}