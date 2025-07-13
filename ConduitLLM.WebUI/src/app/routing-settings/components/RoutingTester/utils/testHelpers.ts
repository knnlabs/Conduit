import { TestRequest, TestResult, TestCase, EvaluationStep } from '../../../types/routing';

/**
 * Validates a test request to ensure all required fields are present
 */
export function validateTestRequest(request: TestRequest): {
  isValid: boolean;
  errors: string[];
} {
  const errors: string[] = [];

  // Required fields validation
  if (!request.model || request.model.trim() === '') {
    errors.push('Model is required');
  }

  // Optional field validation
  if (request.costThreshold !== undefined) {
    if (request.costThreshold < 0) {
      errors.push('Cost threshold cannot be negative');
    }
    if (request.costThreshold > 100) {
      errors.push('Cost threshold seems unreasonably high (>$100)');
    }
  }

  // Virtual key ID format validation
  if (request.virtualKeyId && request.virtualKeyId.trim() !== '') {
    if (request.virtualKeyId.length < 8) {
      errors.push('Virtual key ID should be at least 8 characters');
    }
  }

  // Custom fields validation
  if (request.customFields) {
    Object.entries(request.customFields).forEach(([key, value]) => {
      if (!key.trim()) {
        errors.push('Custom field keys cannot be empty');
      }
      if (value === null || value === undefined) {
        errors.push(`Custom field '${key}' has no value`);
      }
    });
  }

  // Headers validation
  if (request.headers) {
    Object.entries(request.headers).forEach(([key, value]) => {
      if (!key.trim()) {
        errors.push('Header names cannot be empty');
      }
      if (!value || value.trim() === '') {
        errors.push(`Header '${key}' has no value`);
      }
    });
  }

  return {
    isValid: errors.length === 0,
    errors,
  };
}

/**
 * Generates sample test requests for common scenarios
 */
export function getSampleTestRequests(): TestCase[] {
  return [
    {
      id: 'sample-1',
      name: 'GPT-4 Premium Request',
      description: 'Test premium model routing with high-cost threshold',
      timestamp: new Date().toISOString(),
      request: {
        model: 'gpt-4',
        region: 'us-east-1',
        costThreshold: 0.1,
        customFields: {},
        headers: { 'X-Priority': 'high' },
        metadata: { 'request_type': 'premium' },
      },
    },
    {
      id: 'sample-2',
      name: 'Claude Budget Request',
      description: 'Test cost-optimized routing for budget requests',
      timestamp: new Date().toISOString(),
      request: {
        model: 'claude-3-haiku',
        region: 'us-west-2',
        costThreshold: 0.02,
        customFields: { 'priority': 'low', 'batch': 'true' },
        headers: {},
        metadata: {},
      },
    },
    {
      id: 'sample-3',
      name: 'European Regional Request',
      description: 'Test regional routing for EU compliance',
      timestamp: new Date().toISOString(),
      request: {
        model: 'gpt-3.5-turbo',
        region: 'eu-west-1',
        virtualKeyId: 'vk-eu-123456789',
        customFields: { 'gdpr_compliant': 'true' },
        headers: { 'X-Region': 'eu', 'X-Compliance': 'gdpr' },
        metadata: { 'data_residency': 'eu' },
      },
    },
    {
      id: 'sample-4',
      name: 'High-Volume Batch Request',
      description: 'Test load balancing for high-volume scenarios',
      timestamp: new Date().toISOString(),
      request: {
        model: 'llama-2-70b',
        costThreshold: 0.01,
        customFields: { 
          'batch_size': '1000',
          'priority': 'batch',
          'max_tokens': '512'
        },
        headers: { 'X-Batch-Mode': 'true' },
        metadata: { 'processing_type': 'batch' },
      },
    },
    {
      id: 'sample-5',
      name: 'Development Environment Test',
      description: 'Test development environment routing with caching',
      timestamp: new Date().toISOString(),
      request: {
        model: 'gpt-3.5-turbo',
        region: 'us-east-1',
        customFields: { 'environment': 'development' },
        headers: { 'X-Environment': 'development', 'X-Cache': 'enabled' },
        metadata: { 'debug': true, 'cache_ttl': 3600 },
      },
    },
  ];
}

/**
 * Formats test results for export
 */
export function formatTestResultsForExport(
  request: TestRequest,
  result: TestResult
): string {
  const report = {
    summary: {
      timestamp: new Date().toISOString(),
      success: result.success,
      evaluationTime: result.evaluationTime,
      selectedProvider: result.selectedProvider?.name || 'None',
      rulesMatched: result.matchedRules.filter(r => 
        r.matchedConditions.every(c => c.matched)
      ).length,
      rulesApplied: result.matchedRules.filter(r => r.applied).length,
    },
    request: {
      model: request.model,
      region: request.region,
      costThreshold: request.costThreshold,
      virtualKeyId: request.virtualKeyId,
      customFieldsCount: Object.keys(request.customFields).length,
      headersCount: Object.keys(request.headers || {}).length,
    },
    routingDecision: result.routingDecision,
    matchedRules: result.matchedRules.map(rule => ({
      name: rule.rule.name,
      priority: rule.priority,
      applied: rule.applied,
      allConditionsMatched: rule.matchedConditions.every(c => c.matched),
      conditions: rule.matchedConditions.map(c => ({
        type: c.condition.type,
        operator: c.condition.operator,
        expected: c.condition.value,
        actual: c.actualValue,
        matched: c.matched,
        reason: c.reason,
      })),
      actions: rule.rule.actions.map(a => ({
        type: a.type,
        target: a.target,
        parameters: a.parameters,
      })),
    })),
    providerSelection: {
      selected: result.selectedProvider ? {
        id: result.selectedProvider.id,
        name: result.selectedProvider.name,
        type: result.selectedProvider.type,
        priority: result.selectedProvider.priority,
      } : null,
      fallbackChain: result.fallbackChain.map(p => ({
        id: p.id,
        name: p.name,
        type: p.type,
        priority: p.priority,
      })),
    },
    evaluationSteps: result.evaluationSteps.map(step => ({
      stepNumber: step.stepNumber,
      action: step.action,
      details: step.details,
      success: step.success,
      duration: step.duration,
      ruleName: step.ruleName,
    })),
    errors: result.errors || [],
  };

  return JSON.stringify(report, null, 2);
}

/**
 * Calculates test result statistics
 */
export function calculateTestStatistics(results: TestResult[]): {
  totalTests: number;
  successRate: number;
  averageEvaluationTime: number;
  mostUsedProvider: string;
  ruleMatchRate: number;
} {
  if (results.length === 0) {
    return {
      totalTests: 0,
      successRate: 0,
      averageEvaluationTime: 0,
      mostUsedProvider: 'None',
      ruleMatchRate: 0,
    };
  }

  const successfulTests = results.filter(r => r.success).length;
  const successRate = (successfulTests / results.length) * 100;

  const totalEvaluationTime = results.reduce((sum, r) => sum + r.evaluationTime, 0);
  const averageEvaluationTime = totalEvaluationTime / results.length;

  const providerCounts = results.reduce((acc, r) => {
    if (r.selectedProvider) {
      acc[r.selectedProvider.name] = (acc[r.selectedProvider.name] || 0) + 1;
    }
    return acc;
  }, {} as Record<string, number>);

  const mostUsedProvider = Object.entries(providerCounts)
    .sort(([,a], [,b]) => b - a)[0]?.[0] || 'None';

  const testsWithMatches = results.filter(r => 
    r.matchedRules.some(rule => rule.matchedConditions.every(c => c.matched))
  ).length;
  const ruleMatchRate = (testsWithMatches / results.length) * 100;

  return {
    totalTests: results.length,
    successRate,
    averageEvaluationTime,
    mostUsedProvider,
    ruleMatchRate,
  };
}

/**
 * Compares two test results to identify differences
 */
export function compareTestResults(
  result1: TestResult,
  result2: TestResult
): {
  providerChanged: boolean;
  rulesChanged: boolean;
  performanceChanged: boolean;
  differences: string[];
} {
  const differences: string[] = [];
  let providerChanged = false;
  let rulesChanged = false;
  let performanceChanged = false;

  // Compare selected providers
  if (result1.selectedProvider?.id !== result2.selectedProvider?.id) {
    providerChanged = true;
    differences.push(
      `Provider changed from ${result1.selectedProvider?.name || 'None'} to ${result2.selectedProvider?.name || 'None'}`
    );
  }

  // Compare matched rules
  const rules1 = result1.matchedRules.filter(r => r.applied).map(r => r.rule.id);
  const rules2 = result2.matchedRules.filter(r => r.applied).map(r => r.rule.id);
  
  if (JSON.stringify(rules1.sort()) !== JSON.stringify(rules2.sort())) {
    rulesChanged = true;
    differences.push('Applied rules changed between tests');
  }

  // Compare performance
  const timeDiff = Math.abs(result1.evaluationTime - result2.evaluationTime);
  if (timeDiff > 1) { // More than 1ms difference
    performanceChanged = true;
    differences.push(
      `Evaluation time changed by ${timeDiff.toFixed(2)}ms (${result1.evaluationTime.toFixed(2)}ms → ${result2.evaluationTime.toFixed(2)}ms)`
    );
  }

  return {
    providerChanged,
    rulesChanged,
    performanceChanged,
    differences,
  };
}

/**
 * Generates test scenarios for regression testing
 */
export function generateRegressionTestScenarios(): TestCase[] {
  const scenarios: TestCase[] = [];
  const models = ['gpt-4', 'gpt-3.5-turbo', 'claude-3-opus', 'claude-3-sonnet'];
  const regions = ['us-east-1', 'us-west-2', 'eu-west-1'];
  const costThresholds = [0.01, 0.05, 0.10];

  let id = 1;

  // Generate combinations for comprehensive testing
  models.forEach(model => {
    regions.forEach(region => {
      costThresholds.forEach(cost => {
        scenarios.push({
          id: `regression-${id++}`,
          name: `${model} in ${region} (${cost})`,
          description: `Regression test for ${model} model in ${region} region with $${cost} cost threshold`,
          timestamp: new Date().toISOString(),
          request: {
            model,
            region,
            costThreshold: cost,
            customFields: {},
            headers: {},
            metadata: {},
          },
        });
      });
    });
  });

  return scenarios;
}