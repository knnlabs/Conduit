'use client';

import { useState, useCallback } from 'react';
import { notifications } from '@mantine/notifications';
import { 
  RoutingRule, 
  CreateRoutingRuleRequest, 
  UpdateRoutingRuleRequest,
  RouteTestRequest,
  RouteTestResult 
} from '../types/routing';

export function useRoutingRules() {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const getRules = useCallback(async (): Promise<RoutingRule[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // TODO: Replace with actual API call when backend is implemented
      // Simulating API call with mock data
      await new Promise(resolve => setTimeout(resolve, 500));
      
      const mockRules: RoutingRule[] = [
        {
          id: '1',
          name: 'High Priority Models',
          description: 'Route GPT-4 and Claude requests to premium providers',
          priority: 1,
          isEnabled: true,
          conditions: [
            {
              type: 'model',
              operator: 'contains',
              value: 'gpt-4'
            }
          ],
          actions: [
            {
              type: 'route',
              target: 'openai-premium'
            }
          ],
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString()
        }
      ];

      return mockRules;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch routing rules';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const createRule = useCallback(async (rule: CreateRoutingRuleRequest): Promise<RoutingRule> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // TODO: Replace with actual API call when backend is implemented
      await new Promise(resolve => setTimeout(resolve, 500));
      
      const newRule: RoutingRule = {
        id: Date.now().toString(),
        name: rule.name,
        description: rule.description,
        priority: rule.priority || 1,
        isEnabled: rule.enabled || false,
        conditions: rule.conditions || [],
        actions: rule.actions || [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      };

      notifications.show({
        title: 'Success',
        message: 'Routing rule created successfully (mock)',
        color: 'green',
      });

      return newRule;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create routing rule';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const updateRule = useCallback(async (id: string, rule: UpdateRoutingRuleRequest): Promise<RoutingRule> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/config/routing/rules/${id}`, {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(rule),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to update routing rule');
      }

      notifications.show({
        title: 'Success',
        message: 'Routing rule updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update routing rule';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const deleteRule = useCallback(async (id: string): Promise<void> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`/api/config/routing/rules/${id}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const result = await response.json();
        throw new Error(result.error || 'Failed to delete routing rule');
      }

      notifications.show({
        title: 'Success',
        message: 'Routing rule deleted successfully',
        color: 'green',
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete routing rule';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const toggleRule = useCallback(async (id: string, enabled: boolean): Promise<RoutingRule> => {
    return updateRule(id, { enabled });
  }, [updateRule]);

  const testRoute = useCallback(async (testRequest: RouteTestRequest): Promise<RouteTestResult> => {
    setIsLoading(true);
    setError(null);
    
    try {
      // TODO: Replace with actual API call when backend is implemented
      // Simulating API call with mock data
      await new Promise(resolve => setTimeout(resolve, 800));
      
      const mockResult: RouteTestResult = {
        success: true,
        selectedProvider: 'openai-premium',
        routingDecision: {
          strategy: 'priority',
          reason: 'Matched rule: High Priority Models',
          processingTimeMs: 15,
          fallbackUsed: false
        },
        matchedRules: [
          {
            id: '1',
            name: 'High Priority Models',
            priority: 1,
            isEnabled: true,
            conditions: [
              {
                type: 'model',
                operator: 'contains',
                value: 'gpt-4'
              }
            ],
            actions: [
              {
                type: 'route',
                target: 'openai-premium'
              }
            ],
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString()
          }
        ],
        errors: []
      };

      return mockResult;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to test routing rules';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const bulkUpdateRules = useCallback(async (rules: RoutingRule[]): Promise<RoutingRule[]> => {
    setIsLoading(true);
    setError(null);
    
    try {
      const response = await fetch('/api/config/routing/rules', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ rules }),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || 'Failed to update routing rules');
      }

      notifications.show({
        title: 'Success',
        message: 'Routing rules updated successfully',
        color: 'green',
      });

      return result;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update routing rules';
      setError(message);
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    getRules,
    createRule,
    updateRule,
    deleteRule,
    toggleRule,
    testRoute,
    bulkUpdateRules,
    isLoading,
    error,
  };
}