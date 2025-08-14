'use client';

import { useState, useEffect, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { 
  TestRequest, 
  TestResult, 
  TestCase, 
  TestHistory,
  MatchedRule,
  EvaluationStep,
  Provider,
  ConditionMatch,
} from '../../../types/routing';

const STORAGE_KEY = 'routing-test-history';
const MAX_HISTORY_ITEMS = 10;

export function useRoutingTest() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [testHistory, setTestHistory] = useState<TestHistory>({ cases: [] });

  // Load history from localStorage on mount
  useEffect(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored) as TestHistory;
        setTestHistory(parsed);
      }
    } catch (err) {
      console.warn('Failed to load test history:', err);
    }
  }, []);

  // Save history to localStorage whenever it changes
  const saveHistoryToStorage = useCallback((history: TestHistory) => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(history));
    } catch (err) {
      console.warn('Failed to save test history:', err);
    }
  }, []);

  // Mock data generation functions
  const generateMockEvaluationSteps = (request: TestRequest): EvaluationStep[] => {
    const steps: EvaluationStep[] = [];
    const timestamp = Date.now();
    
    steps.push({
      timestamp: timestamp,
      stepNumber: 1,
      action: 'initialization.start',
      details: 'Started routing evaluation process',
      success: true,
      duration: 0.5,
    });

    steps.push({
      timestamp: timestamp + 1,
      stepNumber: 2,
      action: 'validation.request',
      details: `Validating request parameters for model: ${request.model}`,
      success: true,
      duration: 0.8,
    });

    steps.push({
      timestamp: timestamp + 2,
      stepNumber: 3,
      action: 'rules.load',
      details: 'Loading active routing rules from configuration',
      success: true,
      duration: 1.2,
    });

    steps.push({
      timestamp: timestamp + 3,
      stepNumber: 4,
      action: 'rules.evaluate',
      details: 'Evaluating rules against request parameters',
      success: true,
      duration: 2.1,
      ruleName: 'Premium Model Routing',
    });

    steps.push({
      timestamp: timestamp + 4,
      stepNumber: 5,
      action: 'provider.select',
      details: 'Selecting provider based on matched rules and priorities',
      success: true,
      duration: 0.6,
    });

    steps.push({
      timestamp: timestamp + 5,
      stepNumber: 6,
      action: 'fallback.configure',
      details: 'Configuring fallback chain for selected provider',
      success: true,
      duration: 0.4,
    });

    return steps;
  };

  const generateMockMatchedRules = (request: TestRequest): MatchedRule[] => {
    const rules: MatchedRule[] = [];

    // Mock rule 1: Model-based routing
    if (request.model.includes('gpt-4') || request.model.includes('claude')) {
      const conditions: ConditionMatch[] = [
        {
          condition: {
            type: 'model',
            operator: 'in_list',
            value: 'gpt-4, claude-3-opus, claude-3-sonnet',
          },
          matched: true,
          actualValue: request.model,
          reason: 'Model is in premium list',
        },
      ];

      rules.push({
        rule: {
          id: 'rule-1',
          name: 'Premium Model Routing',
          description: 'Route expensive models to primary provider',
          priority: 10,
          isEnabled: true,
          conditions: conditions.map(c => c.condition),
          actions: [
            {
              type: 'route',
              target: 'openai-primary',
              parameters: { fallbackEnabled: true, timeout: 30000 },
            },
          ],
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        matchedConditions: conditions,
        applied: true,
        priority: 10,
      });
    }

    // Mock rule 2: Cost optimization
    if (request.costThreshold !== undefined && request.costThreshold < 0.05) {
      const conditions: ConditionMatch[] = [
        {
          condition: {
            type: 'cost',
            operator: 'less_than',
            value: '0.05',
          },
          matched: true,
          actualValue: request.costThreshold,
          reason: 'Cost threshold is below limit',
        },
      ];

      rules.push({
        rule: {
          id: 'rule-2',
          name: 'Cost Optimization',
          description: 'Route low-cost requests to budget provider',
          priority: 20,
          isEnabled: true,
          conditions: conditions.map(c => c.condition),
          actions: [
            {
              type: 'route',
              target: 'anthropic-secondary',
              parameters: { fallbackEnabled: true },
            },
          ],
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        matchedConditions: conditions,
        applied: false,
        priority: 20,
      });
    }

    // Mock rule 3: Region-based routing
    if (request.region) {
      const conditions: ConditionMatch[] = [
        {
          condition: {
            type: 'region',
            operator: 'equals',
            value: request.region,
          },
          matched: true,
          actualValue: request.region,
          reason: 'Region matches exactly',
        },
      ];

      rules.push({
        rule: {
          id: 'rule-3',
          name: 'Regional Routing',
          description: 'Route based on geographic region',
          priority: 15,
          isEnabled: true,
          conditions: conditions.map(c => c.condition),
          actions: [
            {
              type: 'route',
              target: `provider-${request.region}`,
              parameters: { fallbackEnabled: true },
            },
          ],
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        matchedConditions: conditions,
        applied: false,
        priority: 15,
      });
    }

    return rules;
  };

  const generateMockProviders = (): Provider[] => {
    return [
      {
        id: 'openai-primary',
        name: 'OpenAI Primary',
        type: 'primary',
        isEnabled: true,
        priority: 1,
        endpoint: 'https://api.openai.com/v1',
      },
      {
        id: 'anthropic-primary',
        name: 'Anthropic Primary',
        type: 'primary',
        isEnabled: true,
        priority: 2,
        endpoint: 'https://api.anthropic.com/v1',
      },
      {
        id: 'azure-openai-backup',
        name: 'Azure OpenAI',
        type: 'backup',
        isEnabled: true,
        priority: 3,
        endpoint: 'https://your-resource.openai.azure.com',
      },
    ];
  };

  const runTest = useCallback(async (request: TestRequest): Promise<TestResult> => {
    setIsLoading(true);
    setError(null);

    try {
      // Simulate API delay
      await new Promise(resolve => setTimeout(resolve, 1000 + Math.random() * 1000));

      const evaluationSteps = generateMockEvaluationSteps(request);
      const matchedRules = generateMockMatchedRules(request);
      const providers = generateMockProviders();
      
      // Select provider based on rules
      const appliedRule = matchedRules.find(r => r.applied);
      const selectedProvider = appliedRule 
        ? providers.find(p => p.id === appliedRule.rule.actions[0]?.target) ?? providers[0]
        : providers[0];

      // Generate fallback chain
      const fallbackChain = providers
        .filter(p => p.id !== selectedProvider?.id && p.isEnabled)
        .sort((a, b) => a.priority - b.priority)
        .slice(0, 2);

      const totalEvaluationTime = evaluationSteps.reduce((sum, step) => sum + step.duration, 0);

      const result: TestResult = {
        matchedRules,
        selectedProvider,
        fallbackChain,
        evaluationTime: totalEvaluationTime,
        evaluationSteps,
        routingDecision: {
          strategy: appliedRule ? 'priority' : 'round_robin',
          reason: appliedRule 
            ? `Selected based on rule: ${appliedRule.rule.name}`
            : 'No specific rules matched, using default strategy',
          fallbackUsed: false,
          processingTimeMs: totalEvaluationTime,
        },
        success: true,
        errors: [],
      };

      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred';
      setError(errorMessage);
      
      notifications.show({
        title: 'Test Failed',
        message: errorMessage,
        color: 'red',
      });

      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const saveTestCase = useCallback(async (testCase: TestCase) => {
    const newHistory: TestHistory = {
      cases: [testCase, ...testHistory.cases].slice(0, MAX_HISTORY_ITEMS),
      lastRun: new Date().toISOString(),
    };
    
    setTestHistory(newHistory);
    saveHistoryToStorage(newHistory);
  }, [testHistory, saveHistoryToStorage]);

  const loadTestCase = useCallback((testCase: TestCase) => {
    // This function is handled by the parent component
    // Just return the test case for now
    return testCase;
  }, []);

  const clearHistory = useCallback(() => {
    const emptyHistory: TestHistory = { cases: [] };
    setTestHistory(emptyHistory);
    saveHistoryToStorage(emptyHistory);
    
    notifications.show({
      title: 'History Cleared',
      message: 'All test history has been cleared',
      color: 'blue',
    });
  }, [saveHistoryToStorage]);

  const exportResults = useCallback(async (result: TestResult, request: TestRequest) => {
    const exportData = {
      timestamp: new Date().toISOString(),
      request,
      result,
      metadata: {
        version: '1.0',
        generatedBy: 'Conduit Routing Tester',
      },
    };

    const dataStr = JSON.stringify(exportData, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `routing-test-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }, []);

  return {
    runTest,
    isLoading,
    error,
    testHistory,
    saveTestCase,
    loadTestCase,
    clearHistory,
    exportResults,
  };
}