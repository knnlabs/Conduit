import { SettingsService } from '../services/SettingsService';
import { ENDPOINTS } from '../constants';
import { ValidationError } from '../utils/errors';
import type { RouterConfigurationDto, RouterRule } from '../models/settings';
import { FetchBaseApiClient } from '../client/FetchBaseApiClient';

describe('SettingsService - Routing Methods', () => {
  let service: SettingsService;
  let mockGet: jest.Mock;
  let mockPut: jest.Mock;

  const mockRouterConfig: RouterConfigurationDto = {
    routingStrategy: 'priority',
    fallbackEnabled: true,
    maxRetries: 3,
    retryDelay: 1000,
    loadBalancingEnabled: true,
    healthCheckEnabled: true,
    healthCheckInterval: 30,
    circuitBreakerEnabled: true,
    circuitBreakerThreshold: 5,
    circuitBreakerDuration: 60,
    customRules: [
      {
        id: 1,
        name: 'GPT-4 Priority',
        condition: { type: 'model', operator: 'equals', value: 'gpt-4' },
        action: { type: 'route_to_provider', value: 'openai' },
        priority: 10,
        isEnabled: true,
      },
      {
        id: 2,
        name: 'Rate Limit Rule',
        condition: { type: 'cost', operator: 'greater_than', value: 100 },
        action: { type: 'rate_limit', value: { limit: 10, period: 'minute' } },
        priority: 5,
        isEnabled: true,
      },
    ],
    createdAt: '2025-01-11T10:00:00Z',
    updatedAt: '2025-01-11T10:00:00Z',
  };

  beforeEach(() => {
    mockGet = jest.fn();
    mockPut = jest.fn();
    
    // Mock the FetchBaseApiClient prototype methods
    jest.spyOn(FetchBaseApiClient.prototype as any, 'get').mockImplementation(mockGet);
    jest.spyOn(FetchBaseApiClient.prototype as any, 'put').mockImplementation(mockPut);
    jest.spyOn(FetchBaseApiClient.prototype as any, 'withCache').mockImplementation(async (...args: any[]) => {
      const fn = args[1];
      return await fn();
    });
    
    service = new SettingsService({
      baseUrl: 'http://test.com',
      masterKey: 'test-key',
    });
    
    // Mock the invalidateCache method
    (service as any).invalidateCache = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('getRouterConfiguration', () => {
    it('should get router configuration', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);

      const result = await service.getRouterConfiguration();

      expect(mockGet).toHaveBeenCalledWith(ENDPOINTS.SETTINGS.ROUTER);
      expect(result).toEqual(mockRouterConfig);
    });
  });

  describe('updateRouterConfiguration', () => {
    it('should update router configuration', async () => {
      const updateRequest = { fallbackEnabled: false };
      const updatedConfig = { ...mockRouterConfig, ...updateRequest };
      mockPut.mockResolvedValue(updatedConfig);

      const result = await service.updateRouterConfiguration(updateRequest);

      expect(mockPut).toHaveBeenCalledWith(ENDPOINTS.SETTINGS.ROUTER, updateRequest);
      expect(result).toEqual(updatedConfig);
    });
  });

  describe('createRouterRule', () => {
    it('should create a new router rule', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);
      mockPut.mockResolvedValue(mockRouterConfig);

      const newRule = {
        name: 'New Rule',
        condition: { type: 'model' as const, operator: 'contains' as const, value: 'claude' },
        action: { type: 'route_to_provider' as const, value: 'anthropic' },
        priority: 8,
        isEnabled: true,
      };

      const result = await service.createRouterRule(newRule);

      expect(mockGet).toHaveBeenCalledWith(ENDPOINTS.SETTINGS.ROUTER);
      expect(mockPut).toHaveBeenCalled();
      expect(result).toMatchObject({
        ...newRule,
        id: 3, // Should be max existing ID + 1
      });
    });

    it('should handle empty rules array', async () => {
      const configWithoutRules = { ...mockRouterConfig, customRules: undefined };
      mockGet.mockResolvedValue(configWithoutRules);
      mockPut.mockResolvedValue(configWithoutRules);

      const newRule = {
        name: 'First Rule',
        condition: { type: 'model' as const, operator: 'equals' as const, value: 'gpt-3.5' },
        action: { type: 'block' as const, value: null },
        priority: 1,
        isEnabled: true,
      };

      const result = await service.createRouterRule(newRule);

      expect(result.id).toBe(1);
    });
  });

  describe('updateRouterRule', () => {
    it('should update an existing router rule', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);
      mockPut.mockResolvedValue(mockRouterConfig);

      const update = { name: 'Updated Rule Name', priority: 15 };
      const result = await service.updateRouterRule(1, update);

      expect(mockGet).toHaveBeenCalledWith(ENDPOINTS.SETTINGS.ROUTER);
      expect(result).toMatchObject({
        id: 1,
        name: 'Updated Rule Name',
        priority: 15,
      });
    });

    it('should throw error for non-existent rule', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);

      await expect(service.updateRouterRule(99, { name: 'Test' }))
        .rejects.toThrow(ValidationError);
    });
  });

  describe('deleteRouterRule', () => {
    it('should delete a router rule', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);
      mockPut.mockResolvedValue(mockRouterConfig);

      await service.deleteRouterRule(1);

      expect(mockGet).toHaveBeenCalledWith(ENDPOINTS.SETTINGS.ROUTER);
      expect(mockPut).toHaveBeenCalled();
      
      const updateCall = mockPut.mock.calls[0];
      const updatedRules = updateCall[1].customRules;
      expect(updatedRules).toHaveLength(1);
      expect(updatedRules.find((r: RouterRule) => r.id === 1)).toBeUndefined();
    });

    it('should throw error for non-existent rule', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);

      await expect(service.deleteRouterRule(99))
        .rejects.toThrow(ValidationError);
    });
  });

  describe('reorderRouterRules', () => {
    it('should reorder router rules', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);
      mockPut.mockResolvedValue(mockRouterConfig);

      const result = await service.reorderRouterRules([2, 1]);

      expect(result).toHaveLength(2);
      expect(result[0].id).toBe(2);
      expect(result[0].priority).toBe(2); // Higher priority
      expect(result[1].id).toBe(1);
      expect(result[1].priority).toBe(1); // Lower priority
    });

    it('should throw error for invalid rule ID', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);

      await expect(service.reorderRouterRules([1, 99]))
        .rejects.toThrow(ValidationError);
    });

    it('should handle partial reordering', async () => {
      mockGet.mockResolvedValue(mockRouterConfig);
      mockPut.mockResolvedValue(mockRouterConfig);

      const result = await service.reorderRouterRules([2]);

      expect(result).toHaveLength(2);
      expect(result[0].id).toBe(2);
      expect(result[0].priority).toBe(1);
      expect(result[1].id).toBe(1);
      expect(result[1].priority).toBeLessThan(0); // Negative priority for non-reordered
    });
  });

  describe('testRouterRule', () => {
    it('should validate a valid rule', async () => {
      const rule: RouterRule = {
        name: 'Test Rule',
        condition: { type: 'model', operator: 'equals', value: 'gpt-4' },
        action: { type: 'route_to_provider', value: 'openai' },
        priority: 10,
        isEnabled: true,
      };

      const result = await service.testRouterRule(rule);

      expect(result.success).toBe(true);
      expect(result.message).toBe('Rule validation passed');
      expect(result.details).toBeDefined();
    });

    it('should fail validation for rule without name', async () => {
      const rule: RouterRule = {
        name: '',
        condition: { type: 'model', operator: 'equals', value: 'gpt-4' },
        action: { type: 'route_to_provider', value: 'openai' },
        priority: 10,
        isEnabled: true,
      };

      const result = await service.testRouterRule(rule);

      expect(result.success).toBe(false);
      expect(result.message).toBe('Rule name is required');
    });

    it('should fail validation for invalid condition', async () => {
      const rule: RouterRule = {
        name: 'Test Rule',
        condition: {} as any,
        action: { type: 'route_to_provider', value: 'openai' },
        priority: 10,
        isEnabled: true,
      };

      const result = await service.testRouterRule(rule);

      expect(result.success).toBe(false);
      expect(result.message).toBe('Rule condition is invalid');
    });

    it('should fail validation for invalid action', async () => {
      const rule: RouterRule = {
        name: 'Test Rule',
        condition: { type: 'model', operator: 'equals', value: 'gpt-4' },
        action: {} as any,
        priority: 10,
        isEnabled: true,
      };

      const result = await service.testRouterRule(rule);

      expect(result.success).toBe(false);
      expect(result.message).toBe('Rule action is invalid');
    });
  });
});