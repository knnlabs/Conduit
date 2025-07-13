'use client';

import { useState, useEffect } from 'react';
import {
  Card,
  Stack,
  Group,
  Button,
  Text,
  Alert,
  Title,
} from '@mantine/core';
import {
  IconTestPipe,
  IconAlertCircle,
  IconPlayerPlay,
} from '@tabler/icons-react';
import { RuleTester } from './RuleTester';
import { useRoutingRules } from '../../hooks/useRoutingRules';
import { RouteTestRequest, RouteTestResult } from '../../types/routing';

interface TestingTabProps {
  onLoadingChange: (loading: boolean) => void;
}

export function TestingTab({ onLoadingChange }: TestingTabProps) {
  const [testResult, setTestResult] = useState<RouteTestResult | null>(null);
  const [lastTest, setLastTest] = useState<RouteTestRequest | null>(null);
  
  const { testRoute, isLoading, error } = useRoutingRules();

  useEffect(() => {
    onLoadingChange(isLoading);
  }, [isLoading, onLoadingChange]);

  const handleTest = async (testRequest: RouteTestRequest) => {
    try {
      const result = await testRoute(testRequest);
      setTestResult(result);
      setLastTest(testRequest);
    } catch (err) {
      // Error is handled by the hook
      setTestResult(null);
    }
  };

  const handleRetryTest = () => {
    if (lastTest) {
      handleTest(lastTest);
    }
  };

  if (error) {
    return (
      <Alert icon={<IconAlertCircle size="1rem" />} title="Error" color="red">
        {error}
      </Alert>
    );
  }

  return (
    <Stack gap="md">
      {/* Header */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div>
            <Title order={4}>Testing & Validation</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Test routing rules with sample requests to validate your configuration
            </Text>
          </div>
          {lastTest && (
            <Button
              leftSection={<IconPlayerPlay size={16} />}
              variant="light"
              onClick={handleRetryTest}
              loading={isLoading}
            >
              Retry Last Test
            </Button>
          )}
        </Group>
      </Card>

      {/* Rule Tester */}
      <RuleTester
        onTest={handleTest}
        testResult={testResult}
        isLoading={isLoading}
      />
    </Stack>
  );
}