'use client';

import { useState } from 'react';
import {
  Card,
  Stack,
  Group,
  TextInput,
  Select,
  JsonInput,
  Button,
  Text,
  Badge,
  Alert,
  Title,
} from '@mantine/core';
import {
  IconPlayerPlay,
  IconAlertTriangle,
  IconCheck,
  IconX,
} from '@tabler/icons-react';
import { RouteTestRequest, RouteTestResult } from '../../types/routing';

interface RuleTesterProps {
  onTest: (request: RouteTestRequest) => void;
  testResult: RouteTestResult | null;
  isLoading: boolean;
}

const httpMethods = [
  { value: 'GET', label: 'GET' },
  { value: 'POST', label: 'POST' },
  { value: 'PUT', label: 'PUT' },
  { value: 'DELETE', label: 'DELETE' },
];

const sampleRequests = [
  {
    name: 'Chat Completion',
    request: {
      method: 'POST' as const,
      path: '/chat/completions',
      headers: { 'Content-Type': 'application/json' },
      body: { model: 'gpt-4', messages: [{ role: 'user', content: 'Hello' }] },
      model: 'gpt-4',
      metadata: {},
    }
  },
  {
    name: 'Image Generation',
    request: {
      method: 'POST' as const,
      path: '/images/generations',
      headers: { 'Content-Type': 'application/json' },
      body: { prompt: 'A sunset over mountains', model: 'dall-e-3' },
      model: 'dall-e-3',
      metadata: {},
    }
  },
  {
    name: 'Text Embedding',
    request: {
      method: 'POST' as const,
      path: '/embeddings',
      headers: { 'Content-Type': 'application/json' },
      body: { input: 'Sample text for embedding', model: 'text-embedding-ada-002' },
      model: 'text-embedding-ada-002',
      metadata: {},
    }
  }
];

export function RuleTester({ onTest, testResult, isLoading }: RuleTesterProps) {
  const [testRequest, setTestRequest] = useState<RouteTestRequest>({
    method: 'POST',
    path: '/chat/completions',
    headers: { 'Content-Type': 'application/json' },
    body: { model: 'gpt-4', messages: [{ role: 'user', content: 'Hello' }] },
    model: 'gpt-4',
  });

  const [headersText, setHeadersText] = useState('{"Content-Type": "application/json"}');
  const [bodyText, setBodyText] = useState('{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}');
  const [metadataText, setMetadataText] = useState('{}');

  const handleSampleSelect = (sample: typeof sampleRequests[0]) => {
    setTestRequest(sample.request);
    setHeadersText(JSON.stringify(sample.request.headers ?? {}, null, 2));
    setBodyText(JSON.stringify(sample.request.body ?? {}, null, 2));
    setMetadataText(JSON.stringify(sample.request.metadata ?? {}, null, 2));
  };

  const handleTest = () => {
    try {
      const headers = headersText ? JSON.parse(headersText) as Record<string, string> : {};
      const body = bodyText ? JSON.parse(bodyText) as Record<string, unknown> : undefined;
      const metadata = metadataText ? JSON.parse(metadataText) as Record<string, unknown> : {};

      const request: RouteTestRequest = {
        ...testRequest,
        headers,
        body,
        metadata: Object.keys(metadata).length > 0 ? metadata : undefined,
      };

      onTest(request);
    } catch (err) {
      // Handle JSON parsing errors
      console.error('Invalid JSON in request data:', err);
    }
  };

  const formatDuration = (ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(2)}s`;
  };

  return (
    <Stack gap="md">
      {/* Test Configuration */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" mb="md">
          <Title order={5}>Test Configuration</Title>
          <Group>
            {sampleRequests.map((sample) => (
              <Button
                key={sample.name}
                variant="light"
                size="xs"
                onClick={() => handleSampleSelect(sample)}
              >
                {sample.name}
              </Button>
            ))}
          </Group>
        </Group>

        <Group grow mb="md">
          <Select
            label="HTTP Method"
            data={httpMethods}
            value={testRequest.method ?? null}
            onChange={(value) => value && setTestRequest(prev => ({ ...prev, method: value as 'GET' | 'POST' | 'PUT' | 'DELETE' }))}
            allowDeselect={false}
          />
          <TextInput
            label="Path"
            placeholder="/chat/completions"
            value={testRequest.path}
            onChange={(e) => setTestRequest(prev => ({ ...prev, path: e.target.value }))}
          />
          <TextInput
            label="Model"
            placeholder="gpt-4"
            value={testRequest.model ?? ''}
            onChange={(e) => setTestRequest(prev => ({ ...prev, model: e.target.value }))}
          />
        </Group>

        <Group grow mb="md">
          <JsonInput
            label="Headers"
            placeholder="{}"
            value={headersText}
            onChange={setHeadersText}
            validationError="Invalid JSON"
            formatOnBlur
            autosize
            minRows={2}
            maxRows={6}
          />
          <JsonInput
            label="Metadata"
            placeholder="{}"
            value={metadataText}
            onChange={setMetadataText}
            validationError="Invalid JSON"
            formatOnBlur
            autosize
            minRows={2}
            maxRows={6}
          />
        </Group>

        <JsonInput
          label="Request Body"
          placeholder="{}"
          value={bodyText}
          onChange={setBodyText}
          validationError="Invalid JSON"
          formatOnBlur
          autosize
          minRows={4}
          maxRows={10}
          mb="md"
        />

        <Group justify="flex-end">
          <Button
            leftSection={<IconPlayerPlay size={16} />}
            onClick={handleTest}
            loading={isLoading}
          >
            Test Routing
          </Button>
        </Group>
      </Card>

      {/* Test Results */}
      {testResult && (
        <Card shadow="sm" p="md" radius="md" withBorder>
          <Title order={5} mb="md">Test Results</Title>
          
          {/* Overall Result */}
          <Alert
            icon={testResult.success ? <IconCheck size="1rem" /> : <IconX size="1rem" />}
            title={testResult.success ? 'Test Passed' : 'Test Failed'}
            color={testResult.success ? 'green' : 'red'}
            mb="md"
          >
            <Group>
              <Text size="sm">
                Processing time: {formatDuration(testResult.routingDecision.processingTimeMs)}
              </Text>
              {testResult.selectedProvider && (
                <Badge variant="light" color="blue">
                  → {testResult.selectedProvider}
                </Badge>
              )}
            </Group>
          </Alert>

          {/* Routing Decision */}
          <Card withBorder p="sm" mb="md">
            <Text fw={500} size="sm" mb="xs">Routing Decision</Text>
            <Stack gap="xs">
              <Group justify="space-between">
                <Text size="xs" c="dimmed">Strategy:</Text>
                <Badge variant="outline" size="xs">
                  {testResult.routingDecision.strategy}
                </Badge>
              </Group>
              <Group justify="space-between">
                <Text size="xs" c="dimmed">Reason:</Text>
                <Text size="xs">{testResult.routingDecision.reason}</Text>
              </Group>
              <Group justify="space-between">
                <Text size="xs" c="dimmed">Fallback Used:</Text>
                <Badge 
                  variant="light" 
                  color={testResult.routingDecision.fallbackUsed ? 'orange' : 'gray'}
                  size="xs"
                >
                  {testResult.routingDecision.fallbackUsed ? 'Yes' : 'No'}
                </Badge>
              </Group>
            </Stack>
          </Card>

          {/* Matched Rules */}
          {testResult.matchedRules.length > 0 && (
            <Card withBorder p="sm" mb="md">
              <Text fw={500} size="sm" mb="xs">Matched Rules ({testResult.matchedRules.length})</Text>
              <Stack gap="xs">
                {testResult.matchedRules.map((rule) => (
                  <Group key={rule.id} justify="space-between">
                    <Group>
                      <Badge variant="dot" color="green" size="xs">
                        {rule.name}
                      </Badge>
                      <Text size="xs" c="dimmed">
                        Priority: {rule.priority}
                      </Text>
                    </Group>
                    <Badge variant="outline" size="xs">
                      {rule.isEnabled ? 'Active' : 'Disabled'}
                    </Badge>
                  </Group>
                ))}
              </Stack>
            </Card>
          )}

          {/* Errors */}
          {testResult.errors && testResult.errors.length > 0 && (
            <Alert
              icon={<IconAlertTriangle size="1rem" />}
              title="Errors"
              color="red"
            >
              <Stack gap="xs">
                {testResult.errors.map((error) => (
                  <Text key={`error-${String(error).slice(0, 50).replace(/\s+/g, '-')}`} size="sm">{error}</Text>
                ))}
              </Stack>
            </Alert>
          )}
        </Card>
      )}
    </Stack>
  );
}