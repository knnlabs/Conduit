'use client';

import { useState } from 'react';
import {
  Card,
  Stack,
  Group,
  TextInput,
  Select,
  NumberInput,
  Button,
  Title,
  Text,
  Grid,
  ActionIcon,
  Tooltip,
  Badge,
  Accordion,
  Textarea,
} from '@mantine/core';
import {
  IconFlask,
  IconRefresh,
  IconPlus,
  IconTrash,
  IconHistory,
} from '@tabler/icons-react';
import { TestRequest, TestCase } from '../../../types/routing';

interface TestFormProps {
  request: TestRequest;
  onRequestChange: (request: TestRequest) => void;
  onRunTest: () => void;
  onClear: () => void;
  isLoading: boolean;
  selectedCase?: TestCase | null;
}

export function TestForm({
  request,
  onRequestChange,
  onRunTest,
  onClear,
  isLoading,
  selectedCase,
}: TestFormProps) {
  const [customFieldKey, setCustomFieldKey] = useState('');
  const [customFieldValue, setCustomFieldValue] = useState('');
  const [headerKey, setHeaderKey] = useState('');
  const [headerValue, setHeaderValue] = useState('');

  // Common model options
  const modelOptions = [
    { value: 'gpt-4', label: 'GPT-4' },
    { value: 'gpt-4-turbo', label: 'GPT-4 Turbo' },
    { value: 'gpt-3.5-turbo', label: 'GPT-3.5 Turbo' },
    { value: 'claude-3-opus', label: 'Claude 3 Opus' },
    { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet' },
    { value: 'claude-3-haiku', label: 'Claude 3 Haiku' },
    { value: 'gemini-pro', label: 'Gemini Pro' },
    { value: 'llama-2-70b', label: 'Llama 2 70B' },
    { value: 'mixtral-8x7b', label: 'Mixtral 8x7B' },
  ];

  const regionOptions = [
    { value: 'us-east-1', label: 'US East (N. Virginia)' },
    { value: 'us-west-2', label: 'US West (Oregon)' },
    { value: 'eu-west-1', label: 'EU West (Ireland)' },
    { value: 'eu-central-1', label: 'EU Central (Frankfurt)' },
    { value: 'ap-southeast-1', label: 'Asia Pacific (Singapore)' },
    { value: 'ap-northeast-1', label: 'Asia Pacific (Tokyo)' },
  ];

  const handleAddCustomField = () => {
    if (!customFieldKey.trim() || !customFieldValue.trim()) return;

    onRequestChange({
      ...request,
      customFields: {
        ...request.customFields,
        [customFieldKey]: customFieldValue,
      },
    });

    setCustomFieldKey('');
    setCustomFieldValue('');
  };

  const handleRemoveCustomField = (key: string) => {
    const { [key]: removed, ...rest } = request.customFields;
    onRequestChange({
      ...request,
      customFields: rest,
    });
  };

  const handleAddHeader = () => {
    if (!headerKey.trim() || !headerValue.trim()) return;

    onRequestChange({
      ...request,
      headers: {
        ...request.headers,
        [headerKey]: headerValue,
      },
    });

    setHeaderKey('');
    setHeaderValue('');
  };

  const handleRemoveHeader = (key: string) => {
    if (!request.headers) return;
    const { [key]: removed, ...rest } = request.headers;
    onRequestChange({
      ...request,
      headers: rest,
    });
  };

  const isFormValid = request.model.trim().length > 0;

  return (
    <Card shadow="sm" p="md" radius="md" withBorder>
      <Stack gap="md">
        {/* Header */}
        <Group justify="space-between" align="center">
          <div>
            <Title order={5}>Test Request Parameters</Title>
            <Text size="sm" c="dimmed">
              Configure your test request to evaluate routing rules
            </Text>
          </div>
          {selectedCase && (
            <Badge variant="light" color="blue" leftSection={<IconHistory size={12} />}>
              From: {selectedCase.name}
            </Badge>
          )}
        </Group>

        {/* Core Parameters */}
        <Grid>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Select
              label="Model"
              placeholder="Select a model"
              data={modelOptions}
              value={request.model}
              onChange={(value) => onRequestChange({ ...request, model: value || '' })}
              searchable
              required
              error={!request.model.trim() ? 'Model is required' : null}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Select
              label="Region"
              placeholder="Select a region"
              data={regionOptions}
              value={request.region || ''}
              onChange={(value) => onRequestChange({ ...request, region: value || undefined })}
              searchable
              clearable
            />
          </Grid.Col>
        </Grid>

        <Grid>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <NumberInput
              label="Cost Threshold (USD)"
              placeholder="e.g., 0.05"
              value={request.costThreshold}
              onChange={(value) => onRequestChange({ ...request, costThreshold: typeof value === 'number' ? value : undefined })}
              min={0}
              max={10}
              step={0.01}
              decimalScale={4}
            />
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <TextInput
              label="Virtual Key ID"
              placeholder="Enter virtual key ID"
              value={request.virtualKeyId || ''}
              onChange={(e) => onRequestChange({ ...request, virtualKeyId: e.target.value || undefined })}
            />
          </Grid.Col>
        </Grid>

        {/* Advanced Parameters */}
        <Accordion variant="separated">
          <Accordion.Item value="custom-fields">
            <Accordion.Control>
              <Group>
                <Text fw={500}>Custom Fields</Text>
                <Badge size="sm" variant="light">
                  {Object.keys(request.customFields).length} fields
                </Badge>
              </Group>
            </Accordion.Control>
            <Accordion.Panel>
              <Stack gap="md">
                {/* Add Custom Field */}
                <Group>
                  <TextInput
                    placeholder="Field name"
                    value={customFieldKey}
                    onChange={(e) => setCustomFieldKey(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <TextInput
                    placeholder="Field value"
                    value={customFieldValue}
                    onChange={(e) => setCustomFieldValue(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <Tooltip label="Add custom field">
                    <ActionIcon
                      variant="light"
                      onClick={handleAddCustomField}
                      disabled={!customFieldKey.trim() || !customFieldValue.trim()}
                    >
                      <IconPlus size={16} />
                    </ActionIcon>
                  </Tooltip>
                </Group>

                {/* Existing Custom Fields */}
                {Object.entries(request.customFields).map(([key, value]) => (
                  <Group key={key} justify="space-between" p="xs" bg="gray.0" style={{ borderRadius: 4 }}>
                    <Group>
                      <Text size="sm" fw={500}>{key}:</Text>
                      <Text size="sm">{String(value)}</Text>
                    </Group>
                    <ActionIcon
                      size="sm"
                      variant="subtle"
                      color="red"
                      onClick={() => handleRemoveCustomField(key)}
                    >
                      <IconTrash size={12} />
                    </ActionIcon>
                  </Group>
                ))}
              </Stack>
            </Accordion.Panel>
          </Accordion.Item>

          <Accordion.Item value="headers">
            <Accordion.Control>
              <Group>
                <Text fw={500}>HTTP Headers</Text>
                <Badge size="sm" variant="light">
                  {Object.keys(request.headers || {}).length} headers
                </Badge>
              </Group>
            </Accordion.Control>
            <Accordion.Panel>
              <Stack gap="md">
                {/* Add Header */}
                <Group>
                  <TextInput
                    placeholder="Header name (e.g., X-Environment)"
                    value={headerKey}
                    onChange={(e) => setHeaderKey(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <TextInput
                    placeholder="Header value"
                    value={headerValue}
                    onChange={(e) => setHeaderValue(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <Tooltip label="Add header">
                    <ActionIcon
                      variant="light"
                      onClick={handleAddHeader}
                      disabled={!headerKey.trim() || !headerValue.trim()}
                    >
                      <IconPlus size={16} />
                    </ActionIcon>
                  </Tooltip>
                </Group>

                {/* Existing Headers */}
                {Object.entries(request.headers || {}).map(([key, value]) => (
                  <Group key={key} justify="space-between" p="xs" bg="gray.0" style={{ borderRadius: 4 }}>
                    <Group>
                      <Text size="sm" fw={500}>{key}:</Text>
                      <Text size="sm">{String(value)}</Text>
                    </Group>
                    <ActionIcon
                      size="sm"
                      variant="subtle"
                      color="red"
                      onClick={() => handleRemoveHeader(key)}
                    >
                      <IconTrash size={12} />
                    </ActionIcon>
                  </Group>
                ))}
              </Stack>
            </Accordion.Panel>
          </Accordion.Item>

          <Accordion.Item value="metadata">
            <Accordion.Control>
              <Text fw={500}>Request Metadata (JSON)</Text>
            </Accordion.Control>
            <Accordion.Panel>
              <Textarea
                placeholder="Enter JSON metadata (optional)"
                value={request.metadata ? JSON.stringify(request.metadata, null, 2) : ''}
                onChange={(e) => {
                  try {
                    const metadata = e.target.value ? JSON.parse(e.target.value) : {};
                    onRequestChange({ ...request, metadata });
                  } catch {
                    // Invalid JSON - keep previous value
                  }
                }}
                minRows={3}
                maxRows={8}
                autosize
              />
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion>

        {/* Action Buttons */}
        <Group justify="space-between">
          <Button
            variant="subtle"
            leftSection={<IconRefresh size={16} />}
            onClick={onClear}
            disabled={isLoading}
          >
            Clear Form
          </Button>
          
          <Button
            leftSection={<IconFlask size={16} />}
            onClick={onRunTest}
            disabled={!isFormValid || isLoading}
            loading={isLoading}
          >
            Run Test
          </Button>
        </Group>
      </Stack>
    </Card>
  );
}