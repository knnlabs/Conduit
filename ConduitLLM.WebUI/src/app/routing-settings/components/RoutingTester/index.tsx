'use client';

import { useState, useEffect } from 'react';
import {
  Card,
  Stack,
  Group,
  Text,
  Title,
  Button,
  Grid,
  Alert,
  Loader,
  Center,
  Divider,
} from '@mantine/core';
import {
  IconFlask,
  IconHistory,
  IconDownload,
  IconRefresh,
  IconAlertCircle,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { TestForm } from './components/TestForm';
import { TestResults } from './components/TestResults';
import { RuleEvaluationTrace } from './components/RuleEvaluationTrace';
import { ProviderSelection } from './components/ProviderSelection';
import { TestHistory } from './components/TestHistory';
import { useRoutingTest } from './hooks/useRoutingTest';
import { TestRequest, TestResult, TestCase } from '../../types/routing';

interface RoutingTesterProps {
  onLoadingChange: (loading: boolean) => void;
}

export function RoutingTester({ onLoadingChange }: RoutingTesterProps) {
  const [currentRequest, setCurrentRequest] = useState<TestRequest>({
    model: '',
    region: undefined,
    costThreshold: undefined,
    virtualKeyId: undefined,
    customFields: {},
    headers: {},
    metadata: {},
  });
  
  const [testResult, setTestResult] = useState<TestResult | null>(null);
  const [showHistory, setShowHistory] = useState(false);
  const [selectedHistoryCase, setSelectedHistoryCase] = useState<TestCase | null>(null);

  const {
    runTest,
    isLoading,
    error,
    testHistory,
    saveTestCase,
    loadTestCase,
    clearHistory,
    exportResults,
  } = useRoutingTest();

  useEffect(() => {
    onLoadingChange(isLoading);
  }, [isLoading, onLoadingChange]);

  const handleRunTest = async () => {
    try {
      // Validate required fields
      if (!currentRequest.model.trim()) {
        notifications.show({
          title: 'Validation Error',
          message: 'Model is required to run the test',
          color: 'red',
        });
        return;
      }

      const result = await runTest(currentRequest);
      setTestResult(result);

      // Auto-save successful tests to history
      if (result.success) {
        const testCase: TestCase = {
          id: `test-${Date.now()}`,
          name: `Test: ${currentRequest.model}`,
          request: currentRequest,
          timestamp: new Date().toISOString(),
          description: `Model: ${currentRequest.model}${currentRequest.region ? `, Region: ${currentRequest.region}` : ''}`,
        };
        await saveTestCase(testCase);
      }

    } catch (err) {
      console.error('Test failed:', err);
    }
  };

  const handleLoadTestCase = (testCase: TestCase) => {
    setCurrentRequest(testCase.request);
    setSelectedHistoryCase(testCase);
    setShowHistory(false);
    
    notifications.show({
      title: 'Test Case Loaded',
      message: `Loaded: ${testCase.name}`,
      color: 'green',
    });
  };

  const handleClearForm = () => {
    setCurrentRequest({
      model: '',
      region: undefined,
      costThreshold: undefined,
      virtualKeyId: undefined,
      customFields: {},
      headers: {},
      metadata: {},
    });
    setTestResult(null);
    setSelectedHistoryCase(null);
  };

  const handleExportResults = async () => {
    if (!testResult) {
      notifications.show({
        title: 'No Results',
        message: 'Run a test first to export results',
        color: 'orange',
      });
      return;
    }

    try {
      await exportResults(testResult, currentRequest);
      notifications.show({
        title: 'Export Successful',
        message: 'Test results have been downloaded',
        color: 'green',
      });
    } catch (err) {
      notifications.show({
        title: 'Export Failed',
        message: 'Failed to export test results',
        color: 'red',
      });
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
            <Group align="center" gap="sm">
              <IconFlask size={24} color="blue" />
              <Title order={4}>Test Routing Rules</Title>
            </Group>
            <Text c="dimmed" size="sm" mt={4}>
              Test your routing configuration with sample requests to verify rule behavior
            </Text>
          </div>
          <Group>
            <Button
              leftSection={<IconHistory size={16} />}
              variant="light"
              onClick={() => setShowHistory(!showHistory)}
            >
              {showHistory ? 'Hide History' : 'Show History'}
            </Button>
            {testResult && (
              <Button
                leftSection={<IconDownload size={16} />}
                variant="light"
                onClick={handleExportResults}
              >
                Export Results
              </Button>
            )}
          </Group>
        </Group>
      </Card>

      <Grid>
        {/* Test Form */}
        <Grid.Col span={{ base: 12, md: showHistory ? 8 : 12 }}>
          <Stack gap="md">
            {/* Test Form Card */}
            <TestForm
              request={currentRequest}
              onRequestChange={setCurrentRequest}
              onRunTest={handleRunTest}
              onClear={handleClearForm}
              isLoading={isLoading}
              selectedCase={selectedHistoryCase}
            />

            {/* Loading State */}
            {isLoading && (
              <Card shadow="sm" p="xl" radius="md" withBorder>
                <Center>
                  <Stack align="center" gap="md">
                    <Loader size="lg" />
                    <Text>Evaluating routing rules...</Text>
                  </Stack>
                </Center>
              </Card>
            )}

            {/* Test Results */}
            {testResult && !isLoading && (
              <Stack gap="md">
                <TestResults result={testResult} request={currentRequest} />
                
                <Divider />
                
                {/* Provider Selection Details */}
                <ProviderSelection
                  selectedProvider={testResult.selectedProvider}
                  fallbackChain={testResult.fallbackChain}
                  routingDecision={testResult.routingDecision}
                />
                
                <Divider />
                
                {/* Rule Evaluation Trace */}
                <RuleEvaluationTrace
                  evaluationSteps={testResult.evaluationSteps}
                  matchedRules={testResult.matchedRules}
                  evaluationTime={testResult.evaluationTime}
                />
              </Stack>
            )}
          </Stack>
        </Grid.Col>

        {/* Test History Sidebar */}
        {showHistory && (
          <Grid.Col span={{ base: 12, md: 4 }}>
            <TestHistory
              history={testHistory}
              onLoadTestCase={handleLoadTestCase}
              onClearHistory={clearHistory}
              selectedCase={selectedHistoryCase}
            />
          </Grid.Col>
        )}
      </Grid>
    </Stack>
  );
}