'use client';

import { useState } from 'react';
import { useModels } from '../../../hooks/useModels';
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
  Alert,
} from '@mantine/core';
import {
  IconFlask,
  IconRefresh,
  IconPlus,
  IconTrash,
  IconHistory,
  IconInfoCircle,
  IconHelp,
} from '@tabler/icons-react';
import { TestRequest, TestCase } from '../../../types/routing';
import { getHelpText, getTooltip } from '../../../utils/helpContent';

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
  const { modelOptions, loading: modelsLoading } = useModels();


  // Common AWS regions for routing configuration
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
    const { [key]: removedValue, ...rest } = request.customFields;
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const unusedRemovedValue = removedValue;
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
    const { [key]: removedValue, ...rest } = request.headers;
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    const unusedRemovedValue = removedValue;
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
            <Group align="center" gap="xs">
              <Title order={5}>Test Request Parameters</Title>
              <Tooltip label="Configure test parameters to simulate routing requests and evaluate rule behavior">
                <ActionIcon size="sm" variant="subtle" color="gray">
                  <IconHelp size={14} />
                </ActionIcon>
              </Tooltip>
            </Group>
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

        {/* Help Alert */}
        <Alert icon={<IconInfoCircle size="1rem" />} title="Testing Tips" color="blue" variant="light">
          Use realistic test parameters that match your actual request patterns. 
          The testing interface helps validate routing behavior before applying rules to production traffic.
        </Alert>

        {/* Core Parameters */}
        <Grid>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Tooltip label={getHelpText('testModel')} position="top-start">
              <Select
                label="Model"
                placeholder="Select a model"
                description="Choose the model to test routing rules against"
                data={modelOptions}
                value={request.model || null}
                onChange={(value) => onRequestChange({ ...request, model: value ?? '' })}
                searchable
                disabled={modelsLoading}
                required
                error={!request.model.trim() ? 'Model is required' : null}
                allowDeselect={false}
              />
            </Tooltip>
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Tooltip label={getHelpText('testRegion')} position="top-start">
              <Select
                label="Region"
                placeholder="Select a region"
                description="Choose user or provider region for geographic routing"
                data={regionOptions}
                value={request.region ?? null}
                onChange={(value) => onRequestChange({ ...request, region: value ?? undefined })}
                searchable
                clearable
              />
            </Tooltip>
          </Grid.Col>
        </Grid>

        <Grid>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Tooltip label={getHelpText('testCostThreshold')} position="top-start">
              <NumberInput
                label="Cost Threshold (USD)"
                placeholder="e.g., 0.05"
                description="Maximum acceptable cost per token for testing"
                value={request.costThreshold}
                onChange={(value) => onRequestChange({ ...request, costThreshold: typeof value === 'number' ? value : undefined })}
                min={0}
                max={10}
                step={0.01}
                decimalScale={4}
              />
            </Tooltip>
          </Grid.Col>
          <Grid.Col span={{ base: 12, sm: 6 }}>
            <Tooltip label="Virtual key ID for authentication and tracking" position="top-start">
              <TextInput
                label="Virtual Key ID"
                placeholder="Enter virtual key ID"
                description="Optional virtual key for testing authentication"
                value={request.virtualKeyId ?? ''}
                onChange={(e) => onRequestChange({ ...request, virtualKeyId: e.target.value ?? undefined })}
              />
            </Tooltip>
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
                <Tooltip label={getHelpText('testMetadata')}>
                  <ActionIcon size="sm" variant="subtle" color="gray">
                    <IconHelp size={12} />
                  </ActionIcon>
                </Tooltip>
              </Group>
            </Accordion.Control>
            <Accordion.Panel>
              <Stack gap="md">
                <Text size="sm" c="dimmed">
                  Add custom key-value pairs to simulate request metadata for testing metadata-based routing conditions.
                </Text>
                
                {/* Add Custom Field */}
                <Group>
                  <TextInput
                    placeholder="Field name (e.g., user_tier)"
                    value={customFieldKey}
                    onChange={(e) => setCustomFieldKey(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <TextInput
                    placeholder="Field value (e.g., premium)"
                    value={customFieldValue}
                    onChange={(e) => setCustomFieldValue(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <Tooltip label={getTooltip('addRule')}>
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
                  {Object.keys(request.headers ?? {}).length} headers
                </Badge>
                <Tooltip label={getHelpText('testHeaders')}>
                  <ActionIcon size="sm" variant="subtle" color="gray">
                    <IconHelp size={12} />
                  </ActionIcon>
                </Tooltip>
              </Group>
            </Accordion.Control>
            <Accordion.Panel>
              <Stack gap="md">
                <Text size="sm" c="dimmed">
                  Simulate HTTP headers for testing header-based routing rules. Common examples: X-API-Version, Authorization, User-Agent.
                </Text>
                
                {/* Add Header */}
                <Group>
                  <TextInput
                    placeholder="Header name (e.g., X-Environment)"
                    value={headerKey}
                    onChange={(e) => setHeaderKey(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <TextInput
                    placeholder="Header value (e.g., production)"
                    value={headerValue}
                    onChange={(e) => setHeaderValue(e.target.value)}
                    style={{ flex: 1 }}
                  />
                  <Tooltip label="Add HTTP header for testing">
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
                {Object.entries(request.headers ?? {}).map(([key, value]) => (
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
              <Group>
                <Text fw={500}>Request Metadata (JSON)</Text>
                <Tooltip label="Advanced metadata configuration in JSON format">
                  <ActionIcon size="sm" variant="subtle" color="gray">
                    <IconHelp size={12} />
                  </ActionIcon>
                </Tooltip>
              </Group>
            </Accordion.Control>
            <Accordion.Panel>
              <Stack gap="sm">
                <Text size="sm" c="dimmed">
                  Enter additional metadata in JSON format. This supplements the custom fields above with more complex data structures.
                </Text>
                <Textarea
                  placeholder='{"user_id": "12345", "organization": "acme-corp", "features": ["premium", "analytics"]}'
                  value={request.metadata ? JSON.stringify(request.metadata, null, 2) : ''}
                  onChange={(e) => {
                    try {
                      const metadata = e.target.value ? JSON.parse(e.target.value) as Record<string, unknown> : {};
                      onRequestChange({ ...request, metadata });
                    } catch {
                      // Invalid JSON - keep previous value
                    }
                  }}
                  minRows={3}
                  maxRows={8}
                  autosize
                />
              </Stack>
            </Accordion.Panel>
          </Accordion.Item>
        </Accordion>

        {/* Action Buttons */}
        <Group justify="space-between">
          <Tooltip label={getTooltip('clearTest')}>
            <Button
              variant="subtle"
              leftSection={<IconRefresh size={16} />}
              onClick={onClear}
              disabled={isLoading}
            >
              Clear Form
            </Button>
          </Tooltip>
          
          <Tooltip label={getTooltip('runTest')}>
            <Button
              leftSection={<IconFlask size={16} />}
              onClick={onRunTest}
              disabled={!isFormValid || isLoading}
              loading={isLoading}
            >
              Run Test
            </Button>
          </Tooltip>
        </Group>
      </Stack>
    </Card>
  );
}