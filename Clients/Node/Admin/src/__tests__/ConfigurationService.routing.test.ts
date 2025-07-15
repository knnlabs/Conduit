import { ConfigurationService } from '../services/ConfigurationService';
import { ENDPOINTS } from '../constants';
import { ValidationError } from '../utils/errors';
import type {
  RoutingConfigDto,
  UpdateRoutingConfigDto as UpdateExtendedRoutingConfigDto,
  RoutingRule,
  CreateRoutingRuleDto,
  UpdateRoutingRuleDto,
  LoadBalancerHealthDto,
} from '../models/configurationExtended';

// Mock the entire FetchBaseApiClient module
jest.mock('../client/FetchBaseApiClient');

describe('ConfigurationService - Extended Routing Methods', () => {
  let service: ConfigurationService;
  let mockGet: jest.Mock;
  let mockPut: jest.Mock;
  let mockPost: jest.Mock;
  let mockDelete: jest.Mock;

  const mockRoutingConfig: RoutingConfigDto = {
    defaultStrategy: 'round_robin',
    fallbackEnabled: true,
    retryPolicy: {
      maxAttempts: 3,
      initialDelayMs: 100,
      maxDelayMs: 5000,
      backoffMultiplier: 2,
      retryableStatuses: [502, 503, 504],
    },
    timeoutMs: 30000,
    maxConcurrentRequests: 100,
  };

  const mockRoutingRule: RoutingRule = {
    id: 'rule-1',
    name: 'Model Routing Rule',
    priority: 10,
    conditions: [
      {
        type: 'model',
        operator: 'equals',
        value: 'gpt-4',
      },
    ],
    actions: [
      {
        type: 'route',
        target: 'openai-primary',
      },
    ],
    enabled: true,
    stats: {
      matchCount: 1250,
      lastMatched: '2025-01-11T10:00:00Z',
    },
  };

  const mockLoadBalancerHealth: LoadBalancerHealthDto = {
    status: 'healthy',
    nodes: [
      {
        id: 'node-1',
        endpoint: 'https://api.openai.com',
        status: 'healthy',
        weight: 100,
        activeConnections: 15,
        totalRequests: 50000,
        avgResponseTime: 250,
        lastHealthCheck: '2025-01-11T10:00:00Z',
      },
    ],
    lastCheck: '2025-01-11T10:00:00Z',
    distribution: {
      'openai': 60,
      'anthropic': 40,
    },
  };

  beforeEach(() => {
    mockGet = jest.fn();
    mockPut = jest.fn();
    mockPost = jest.fn();
    mockDelete = jest.fn();
    
    service = new ConfigurationService({
      baseUrl: 'http://test.com',
      masterKey: 'test-key',
    });
    
    // Mock the protected methods
    (service as any).get = mockGet;
    (service as any).put = mockPut;
    (service as any).post = mockPost;
    (service as any).delete = mockDelete;
    (service as any).withCache = jest.fn((key, fn) => fn());
    (service as any).invalidateConfigurationCache = jest.fn();
  });

  describe('extendedRouting.get', () => {
    it('should get extended routing configuration', async () => {
      mockGet.mockResolvedValue(mockRoutingConfig);

      const result = await service.extendedRouting.get();

      expect(mockGet).toHaveBeenCalledWith('/api/config/routing');
      expect(result).toEqual(mockRoutingConfig);
    });
  });

  describe('extendedRouting.update', () => {
    it('should update extended routing configuration', async () => {
      const updateRequest: UpdateExtendedRoutingConfigDto = {
        defaultStrategy: 'least_latency',
        timeoutMs: 45000,
      };
      const updatedConfig = { ...mockRoutingConfig, ...updateRequest };
      mockPut.mockResolvedValue(updatedConfig);

      const result = await service.extendedRouting.update(updateRequest);

      expect(mockPut).toHaveBeenCalledWith('/api/config/routing', updateRequest);
      expect(result).toEqual(updatedConfig);
    });

    it('should validate retry policy updates', async () => {
      const invalidUpdate: UpdateExtendedRoutingConfigDto = {
        retryPolicy: {
          maxAttempts: -1, // Invalid negative value
        },
      };

      await expect(service.extendedRouting.update(invalidUpdate))
        .rejects.toThrow(ValidationError);
    });

    it('should validate timeout values', async () => {
      const invalidUpdate: UpdateExtendedRoutingConfigDto = {
        timeoutMs: -1000, // Invalid negative value
      };

      await expect(service.extendedRouting.update(invalidUpdate))
        .rejects.toThrow(ValidationError);
    });
  });

  describe('extendedRouting.getRules', () => {
    it('should get all routing rules', async () => {
      const mockRules = [mockRoutingRule];
      mockGet.mockResolvedValue(mockRules);

      const result = await service.extendedRouting.getRules();

      expect(mockGet).toHaveBeenCalledWith(ENDPOINTS.CONFIGURATION.ROUTING_RULES);
      expect(result).toEqual(mockRules);
    });
  });

  describe('extendedRouting.createRule', () => {
    it('should create a new routing rule', async () => {
      const createRequest: CreateRoutingRuleDto = {
        name: 'New Routing Rule',
        priority: 5,
        conditions: [
          {
            type: 'header',
            field: 'X-Model-Type',
            operator: 'contains',
            value: 'vision',
          },
        ],
        actions: [
          {
            type: 'cache',
            parameters: { ttl: 300 },
          },
        ],
        enabled: true,
      };
      
      const createdRule = { ...createRequest, id: 'rule-2' };
      mockPost.mockResolvedValue(createdRule);

      const result = await service.extendedRouting.createRule(createRequest);

      expect(mockPost).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        createRequest
      );
      expect(result).toEqual(createdRule);
    });

    it('should validate rule creation parameters', async () => {
      const invalidRule: CreateRoutingRuleDto = {
        name: '', // Empty name
        conditions: [],
        actions: [],
      };

      await expect(service.extendedRouting.createRule(invalidRule))
        .rejects.toThrow(ValidationError);
    });

    it('should validate condition types', async () => {
      const invalidRule: CreateRoutingRuleDto = {
        name: 'Invalid Rule',
        conditions: [
          {
            type: 'invalid-type' as any,
            operator: 'equals',
            value: 'test',
          },
        ],
        actions: [
          {
            type: 'route',
            target: 'provider',
          },
        ],
      };

      await expect(service.extendedRouting.createRule(invalidRule))
        .rejects.toThrow(ValidationError);
    });
  });

  describe('extendedRouting.updateRule', () => {
    it('should update an existing routing rule', async () => {
      const updateRequest: UpdateRoutingRuleDto = {
        name: 'Updated Rule Name',
        priority: 15,
        enabled: false,
      };
      
      const updatedRule = { ...mockRoutingRule, ...updateRequest };
      mockPut.mockResolvedValue(updatedRule);

      const result = await service.extendedRouting.updateRule('rule-1', updateRequest);

      expect(mockPut).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID('rule-1'),
        updateRequest
      );
      expect(result).toEqual(updatedRule);
    });

    it('should throw error for empty rule ID', async () => {
      await expect(service.extendedRouting.updateRule('', { name: 'Test' }))
        .rejects.toThrow(ValidationError);
    });
  });

  describe('extendedRouting.deleteRule', () => {
    it('should delete a routing rule', async () => {
      mockDelete.mockResolvedValue(undefined);

      await service.extendedRouting.deleteRule('rule-1');

      expect(mockDelete).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID('rule-1')
      );
    });

    it('should throw error for empty rule ID', async () => {
      await expect(service.extendedRouting.deleteRule(''))
        .rejects.toThrow(ValidationError);
    });
  });

  describe('extendedRouting.bulkUpdateRules', () => {
    it('should bulk update routing rules', async () => {
      const rules = [mockRoutingRule];
      mockPut.mockResolvedValue(rules);

      const result = await service.extendedRouting.bulkUpdateRules(rules);

      expect(mockPut).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        { rules }
      );
      expect(result).toEqual(rules);
    });
  });

  describe('extendedRouting.getLoadBalancerHealthExtended', () => {
    it('should get extended load balancer health', async () => {
      mockGet.mockResolvedValue(mockLoadBalancerHealth);

      const result = await service.extendedRouting.getLoadBalancerHealthExtended();

      expect(mockGet).toHaveBeenCalledWith('/api/config/loadbalancer/health');
      expect(result).toEqual(mockLoadBalancerHealth);
    });
  });

  describe('subscriptions', () => {
    it('should provide subscription methods', () => {
      expect(service.subscriptions.subscribeToRoutingStatus).toBeDefined();
      expect(service.subscriptions.subscribeToHealthStatus).toBeDefined();
    });

    it('should warn about unimplemented subscriptions', () => {
      const consoleWarnSpy = jest.spyOn(console, 'warn').mockImplementation();
      
      const unsubscribe1 = service.subscriptions.subscribeToRoutingStatus();
      const unsubscribe2 = service.subscriptions.subscribeToHealthStatus();

      expect(consoleWarnSpy).toHaveBeenCalledWith('Real-time subscriptions not yet implemented');
      expect(consoleWarnSpy).toHaveBeenCalledTimes(2);
      expect(typeof unsubscribe1).toBe('function');
      expect(typeof unsubscribe2).toBe('function');

      consoleWarnSpy.mockRestore();
    });
  });
});