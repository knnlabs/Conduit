/**
 * Performance tests for routing rule evaluation
 * Ensures routing decisions complete within acceptable time limits
 */

import { describe, it, expect, beforeEach } from '@jest/globals';

// Mock routing types for testing
interface RoutingRule {
  id: string;
  name: string;
  priority: number;
  enabled: boolean;
  conditions: RoutingCondition[];
  actions: RoutingAction[];
}

interface RoutingCondition {
  type: string;
  field?: string;
  operator: string;
  value: any;
}

interface RoutingAction {
  type: string;
  target?: string;
  value?: any;
}

interface TestRequest {
  model: string;
  region?: string;
  costThreshold?: number;
  metadata?: Record<string, any>;
  headers?: Record<string, string>;
}

// Mock routing engine for performance testing
class MockRoutingEngine {
  private rules: RoutingRule[] = [];

  addRule(rule: RoutingRule): void {
    this.rules.push(rule);
    this.rules.sort((a, b) => a.priority - b.priority);
  }

  evaluateRequest(request: TestRequest): {
    matchedRules: RoutingRule[];
    selectedProvider: string;
    evaluationTimeMs: number;
  } {
    const startTime = performance.now();
    
    const matchedRules: RoutingRule[] = [];
    
    for (const rule of this.rules) {
      if (!rule.enabled) continue;
      
      const ruleMatches = this.evaluateRule(rule, request);
      if (ruleMatches) {
        matchedRules.push(rule);
        break; // First matching rule wins
      }
    }
    
    const endTime = performance.now();
    const evaluationTimeMs = endTime - startTime;
    
    return {
      matchedRules,
      selectedProvider: matchedRules.length > 0 ? 'matched-provider' : 'default-provider',
      evaluationTimeMs
    };
  }

  private evaluateRule(rule: RoutingRule, request: TestRequest): boolean {
    // Simulate condition evaluation
    for (const condition of rule.conditions) {
      if (!this.evaluateCondition(condition, request)) {
        return false;
      }
    }
    return true;
  }

  private evaluateCondition(condition: RoutingCondition, request: TestRequest): boolean {
    // Simulate condition evaluation with some processing time
    const startTime = performance.now();
    
    let result = false;
    
    switch (condition.type) {
      case 'model':
        result = this.evaluateModelCondition(condition, request);
        break;
      case 'region':
        result = this.evaluateRegionCondition(condition, request);
        break;
      case 'cost':
        result = this.evaluateCostCondition(condition, request);
        break;
      case 'metadata':
        result = this.evaluateMetadataCondition(condition, request);
        break;
      default:
        result = true;
    }
    
    // Simulate some processing time
    const endTime = performance.now();
    const processingTime = endTime - startTime;
    
    // Add small delay for complex operations (regex, etc.)
    if (condition.operator === 'regex') {
      // Simulate regex processing
      const pattern = new RegExp(condition.value);
      const testValue = this.getRequestValue(condition, request);
      result = pattern.test(String(testValue));
    }
    
    return result;
  }

  private evaluateModelCondition(condition: RoutingCondition, request: TestRequest): boolean {
    switch (condition.operator) {
      case 'equals':
        return request.model === condition.value;
      case 'contains':
        return request.model.includes(condition.value);
      case 'in':
        return Array.isArray(condition.value) ? condition.value.includes(request.model) : false;
      default:
        return false;
    }
  }

  private evaluateRegionCondition(condition: RoutingCondition, request: TestRequest): boolean {
    if (!request.region) return false;
    
    switch (condition.operator) {
      case 'equals':
        return request.region === condition.value;
      case 'starts_with':
        return request.region.startsWith(condition.value);
      default:
        return false;
    }
  }

  private evaluateCostCondition(condition: RoutingCondition, request: TestRequest): boolean {
    if (request.costThreshold === undefined) return false;
    
    switch (condition.operator) {
      case 'greater_than':
        return request.costThreshold > condition.value;
      case 'less_than':
        return request.costThreshold < condition.value;
      default:
        return false;
    }
  }

  private evaluateMetadataCondition(condition: RoutingCondition, request: TestRequest): boolean {
    if (!request.metadata || !condition.field) return false;
    
    const value = request.metadata[condition.field];
    if (value === undefined) return false;
    
    switch (condition.operator) {
      case 'equals':
        return value === condition.value;
      case 'contains':
        return String(value).includes(condition.value);
      default:
        return false;
    }
  }

  private getRequestValue(condition: RoutingCondition, request: TestRequest): any {
    switch (condition.type) {
      case 'model':
        return request.model;
      case 'region':
        return request.region;
      case 'cost':
        return request.costThreshold;
      case 'metadata':
        return condition.field ? request.metadata?.[condition.field] : undefined;
      default:
        return undefined;
    }
  }

  clear(): void {
    this.rules = [];
  }
}

describe('Routing Performance Tests', () => {
  let engine: MockRoutingEngine;

  beforeEach(() => {
    engine = new MockRoutingEngine();
  });

  describe('Basic Performance Benchmarks', () => {
    it('should evaluate single rule under 5ms', () => {
      // Add a simple rule
      engine.addRule({
        id: 'test-rule-1',
        name: 'Simple Model Rule',
        priority: 10,
        enabled: true,
        conditions: [
          {
            type: 'model',
            operator: 'equals',
            value: 'gpt-4'
          }
        ],
        actions: [
          {
            type: 'route_to_provider',
            target: 'openai-premium'
          }
        ]
      });

      const testRequest: TestRequest = {
        model: 'gpt-4',
        region: 'us-east-1'
      };

      const result = engine.evaluateRequest(testRequest);
      
      expect(result.evaluationTimeMs).toBeLessThan(5);
      expect(result.matchedRules).toHaveLength(1);
    });

    it('should evaluate 10 rules under 10ms', () => {
      // Add 10 rules with different priorities
      for (let i = 0; i < 10; i++) {
        engine.addRule({
          id: `test-rule-${i}`,
          name: `Test Rule ${i}`,
          priority: i * 10,
          enabled: true,
          conditions: [
            {
              type: 'model',
              operator: 'equals',
              value: `model-${i}`
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: `provider-${i}`
            }
          ]
        });
      }

      const testRequest: TestRequest = {
        model: 'model-5', // Should match the 6th rule
        region: 'us-east-1'
      };

      const result = engine.evaluateRequest(testRequest);
      
      expect(result.evaluationTimeMs).toBeLessThan(10);
      expect(result.matchedRules).toHaveLength(1);
      expect(result.matchedRules[0].name).toBe('Test Rule 5');
    });

    it('should evaluate 50 rules under 25ms', () => {
      // Add 50 rules
      for (let i = 0; i < 50; i++) {
        engine.addRule({
          id: `test-rule-${i}`,
          name: `Test Rule ${i}`,
          priority: i,
          enabled: true,
          conditions: [
            {
              type: 'model',
              operator: 'equals',
              value: `model-${i}`
            },
            {
              type: 'region',
              operator: 'starts_with',
              value: 'us-'
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: `provider-${i}`
            }
          ]
        });
      }

      const testRequest: TestRequest = {
        model: 'model-25', // Should match rule 25
        region: 'us-east-1'
      };

      const result = engine.evaluateRequest(testRequest);
      
      expect(result.evaluationTimeMs).toBeLessThan(25);
      expect(result.matchedRules).toHaveLength(1);
    });
  });

  describe('Complex Condition Performance', () => {
    it('should handle complex metadata conditions efficiently', () => {
      engine.addRule({
        id: 'complex-rule',
        name: 'Complex Metadata Rule',
        priority: 10,
        enabled: true,
        conditions: [
          {
            type: 'model',
            operator: 'in',
            value: ['gpt-4', 'gpt-4-turbo', 'claude-3-opus']
          },
          {
            type: 'metadata',
            field: 'user_tier',
            operator: 'equals',
            value: 'premium'
          },
          {
            type: 'metadata',
            field: 'organization',
            operator: 'contains',
            value: 'enterprise'
          },
          {
            type: 'cost',
            operator: 'greater_than',
            value: 0.001
          }
        ],
        actions: [
          {
            type: 'route_to_provider',
            target: 'premium-provider'
          }
        ]
      });

      const testRequest: TestRequest = {
        model: 'gpt-4',
        region: 'us-east-1',
        costThreshold: 0.002,
        metadata: {
          user_tier: 'premium',
          organization: 'enterprise-corp',
          features: ['analytics', 'custom-models']
        }
      };

      const result = engine.evaluateRequest(testRequest);
      
      expect(result.evaluationTimeMs).toBeLessThan(10);
      expect(result.matchedRules).toHaveLength(1);
    });

    it('should handle regex conditions with reasonable performance', () => {
      engine.addRule({
        id: 'regex-rule',
        name: 'Regex Pattern Rule',
        priority: 10,
        enabled: true,
        conditions: [
          {
            type: 'metadata',
            field: 'email',
            operator: 'regex',
            value: '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$'
          }
        ],
        actions: [
          {
            type: 'route_to_provider',
            target: 'email-validated-provider'
          }
        ]
      });

      const testRequest: TestRequest = {
        model: 'gpt-3.5-turbo',
        metadata: {
          email: 'user@example.com'
        }
      };

      const result = engine.evaluateRequest(testRequest);
      
      // Regex should still be reasonably fast
      expect(result.evaluationTimeMs).toBeLessThan(15);
      expect(result.matchedRules).toHaveLength(1);
    });
  });

  describe('Load Testing Scenarios', () => {
    it('should handle 100 rule evaluations consistently', () => {
      // Add 100 rules
      for (let i = 0; i < 100; i++) {
        engine.addRule({
          id: `load-test-rule-${i}`,
          name: `Load Test Rule ${i}`,
          priority: i,
          enabled: true,
          conditions: [
            {
              type: 'model',
              operator: 'equals',
              value: `model-${i}`
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: `provider-${i}`
            }
          ]
        });
      }

      const evaluationTimes: number[] = [];
      
      // Run 10 evaluations to check consistency
      for (let i = 0; i < 10; i++) {
        const testRequest: TestRequest = {
          model: 'model-99', // Should match the last rule (worst case)
          region: 'us-east-1'
        };

        const result = engine.evaluateRequest(testRequest);
        evaluationTimes.push(result.evaluationTimeMs);
      }

      // All evaluations should be under 50ms
      evaluationTimes.forEach(time => {
        expect(time).toBeLessThan(50);
      });

      // Calculate average and ensure consistency
      const averageTime = evaluationTimes.reduce((a, b) => a + b, 0) / evaluationTimes.length;
      const maxTime = Math.max(...evaluationTimes);
      const minTime = Math.min(...evaluationTimes);
      
      expect(averageTime).toBeLessThan(30);
      expect(maxTime - minTime).toBeLessThan(20); // Variance should be reasonable
    });

    it('should maintain performance with disabled rules', () => {
      // Add 50 rules, disable half of them
      for (let i = 0; i < 50; i++) {
        engine.addRule({
          id: `disabled-test-rule-${i}`,
          name: `Disabled Test Rule ${i}`,
          priority: i,
          enabled: i % 2 === 0, // Enable every other rule
          conditions: [
            {
              type: 'model',
              operator: 'equals',
              value: `model-${i}`
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: `provider-${i}`
            }
          ]
        });
      }

      const testRequest: TestRequest = {
        model: 'model-48', // Should match rule 48 (enabled)
        region: 'us-east-1'
      };

      const result = engine.evaluateRequest(testRequest);
      
      // Should skip disabled rules efficiently
      expect(result.evaluationTimeMs).toBeLessThan(25);
      expect(result.matchedRules).toHaveLength(1);
      expect(result.matchedRules[0].name).toBe('Disabled Test Rule 48');
    });
  });

  describe('Performance Regression Prevention', () => {
    it('should maintain sub-10ms performance for typical configurations', () => {
      // Typical production-like configuration
      const typicalRules = [
        {
          id: 'eu-compliance',
          name: 'EU Data Compliance',
          priority: 1,
          enabled: true,
          conditions: [
            {
              type: 'region',
              operator: 'starts_with',
              value: 'eu-'
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: 'eu-provider'
            }
          ]
        },
        {
          id: 'expensive-models',
          name: 'Expensive Model Routing',
          priority: 10,
          enabled: true,
          conditions: [
            {
              type: 'model',
              operator: 'in',
              value: ['gpt-4', 'claude-3-opus']
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: 'premium-provider'
            }
          ]
        },
        {
          id: 'cost-control',
          name: 'Cost Control',
          priority: 20,
          enabled: true,
          conditions: [
            {
              type: 'cost',
              operator: 'greater_than',
              value: 0.002
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: 'cost-effective-provider'
            }
          ]
        },
        {
          id: 'premium-users',
          name: 'Premium User Routing',
          priority: 30,
          enabled: true,
          conditions: [
            {
              type: 'metadata',
              field: 'user_tier',
              operator: 'equals',
              value: 'premium'
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: 'premium-provider'
            }
          ]
        }
      ];

      typicalRules.forEach(rule => engine.addRule(rule));

      const testRequest: TestRequest = {
        model: 'gpt-4',
        region: 'us-east-1',
        costThreshold: 0.003,
        metadata: {
          user_tier: 'premium'
        }
      };

      const result = engine.evaluateRequest(testRequest);
      
      // Should match first rule (expensive models) due to priority
      expect(result.evaluationTimeMs).toBeLessThan(10);
      expect(result.matchedRules).toHaveLength(1);
      expect(result.matchedRules[0].name).toBe('Expensive Model Routing');
    });

    it('should handle no-match scenarios efficiently', () => {
      // Add several rules that won't match
      for (let i = 0; i < 20; i++) {
        engine.addRule({
          id: `no-match-rule-${i}`,
          name: `No Match Rule ${i}`,
          priority: i,
          enabled: true,
          conditions: [
            {
              type: 'model',
              operator: 'equals',
              value: `non-existent-model-${i}`
            }
          ],
          actions: [
            {
              type: 'route_to_provider',
              target: `provider-${i}`
            }
          ]
        });
      }

      const testRequest: TestRequest = {
        model: 'gpt-4', // Won't match any rules
        region: 'us-east-1'
      };

      const result = engine.evaluateRequest(testRequest);
      
      // Should evaluate all rules quickly and find no matches
      expect(result.evaluationTimeMs).toBeLessThan(15);
      expect(result.matchedRules).toHaveLength(0);
      expect(result.selectedProvider).toBe('default-provider');
    });
  });
});