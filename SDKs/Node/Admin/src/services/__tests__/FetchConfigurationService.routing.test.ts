import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchConfigurationService } from '../FetchConfigurationService';
import { ENDPOINTS } from '../../constants';
import type {
  RoutingConfigDto,
  UpdateRoutingConfigDto as ExtendedUpdateRoutingConfigDto,
  RoutingRule as ExtendedRoutingRule,
  CreateRoutingRuleDto,
} from '../../models/configurationExtended';
import type {
  TestResult,
} from '../../models/configuration';

describe('FetchConfigurationService - Routing Configuration', () => {
  let service: FetchConfigurationService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchConfigurationService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Routing Configuration', () => {
    it('should get routing config', async () => {
      const mockConfig: RoutingConfigDto = {
        defaultStrategy: 'round_robin',
        fallbackEnabled: true,
        retryPolicy: {
          maxAttempts: 3,
          initialDelayMs: 100,
          maxDelayMs: 5000,
          backoffMultiplier: 2,
          retryableStatuses: [502, 503, 504]
        },
        timeoutMs: 30000,
        maxConcurrentRequests: 100
      };

      mockClient.get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getRoutingConfig();

      expect(mockClient.get).toHaveBeenCalledWith(
        '/api/config/routing',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockConfig);
    });

    it('should update routing config', async () => {
      const updateData: ExtendedUpdateRoutingConfigDto = {
        defaultStrategy: 'least_latency',
        timeoutMs: 60000
      };

      const mockResponse: RoutingConfigDto = {
        defaultStrategy: 'least_latency',
        fallbackEnabled: true,
        retryPolicy: {
          maxAttempts: 3,
          initialDelayMs: 100,
          maxDelayMs: 5000,
          backoffMultiplier: 2,
          retryableStatuses: [502, 503, 504]
        },
        timeoutMs: 60000,
        maxConcurrentRequests: 100
      };

      mockClient.put = jest.fn().mockResolvedValue(mockResponse);

      const result = await service.updateRoutingConfig(updateData);

      expect(mockClient.put).toHaveBeenCalledWith(
        '/api/config/routing',
        updateData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResponse);
    });

    it('should test routing config', async () => {
      const mockResult: TestResult = {
        success: true,
        executionTimeMs: 150,
        results: [
          { test: 'connectivity', passed: true, message: 'All providers reachable' }
        ],
        errors: []
      };

      mockClient.post = jest.fn().mockResolvedValue(mockResult);

      const result = await service.testRoutingConfig();

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_TEST,
        {},
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockResult);
    });

    it('should get routing rules', async () => {
      const mockRules: ExtendedRoutingRule[] = [
        {
          id: 'rule-1',
          name: 'GPT-4 Priority',
          priority: 1,
          conditions: [{ type: 'model', operator: 'equals', value: 'gpt-4' }],
          actions: [{ type: 'route', target: 'openai' }],
          enabled: true
        }
      ];

      mockClient.get = jest.fn().mockResolvedValue(mockRules);

      const result = await service.getRoutingRules();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockRules);
    });

    it('should create routing rule', async () => {
      const createData: CreateRoutingRuleDto = {
        name: 'High Load Rule',
        priority: 2,
        conditions: [{ type: 'load', operator: 'gt', value: 80 }],
        actions: [{ type: 'route', target: 'backup-provider' }],
        enabled: true
      };

      const mockRule: ExtendedRoutingRule = {
        id: 'rule-2',
        ...createData,
        priority: 1, // Add default priority
        enabled: createData.enabled ?? true, // Ensure enabled is always set
        stats: { matchCount: 0 }
      };

      mockClient.post = jest.fn().mockResolvedValue(mockRule);

      const result = await service.createRoutingRule(createData);

      expect(mockClient.post).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULES,
        createData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockRule);
    });

    it('should delete routing rule', async () => {
      mockClient.delete = jest.fn().mockResolvedValue(undefined);

      await service.deleteRoutingRule('rule-1');

      expect(mockClient.delete).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.ROUTING_RULE_BY_ID('rule-1'),
        { signal: undefined, timeout: undefined, headers: undefined }
      );
    });
  });

  describe('validateRoutingRule', () => {
    it('should validate valid routing rule', () => {
      const rule: CreateRoutingRuleDto = {
        name: 'Valid Rule',
        conditions: [{ type: 'model', operator: 'equals', value: 'gpt-4' }],
        actions: [{ type: 'route', target: 'openai' }]
      };

      const errors = service.validateRoutingRule(rule);
      expect(errors).toHaveLength(0);
    });

    it('should catch validation errors', () => {
      const rule: CreateRoutingRuleDto = {
        name: '',
        conditions: [],
        actions: []
      };

      const errors = service.validateRoutingRule(rule);
      expect(errors).toContain('Rule name is required');
      expect(errors).toContain('At least one condition is required');
      expect(errors).toContain('At least one action is required');
    });
  });
});