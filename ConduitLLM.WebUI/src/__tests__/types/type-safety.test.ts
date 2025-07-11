import type { 
  VirtualKeyDto, 
  ProviderDto,
  ModelMappingDto,
  SystemInfoDto,
  ProviderHealthDto,
} from '@knn_labs/conduit-admin-client';
import type { 
  ChatCompletionRequest,
  ChatCompletionResponse,
  Model,
} from '@knn_labs/conduit-core-client';
import { 
  mapVirtualKeyFromSDK, 
  mapVirtualKeyToSDK,
  mapProviderFromSDK,
  mapProviderToSDK,
  mapModelMappingFromSDK,
  mapModelMappingToSDK,
  type UIVirtualKey,
  type UIProvider,
  type UIModelMapping,
} from '@/lib/types/mappers';

describe('Type Safety', () => {
  describe('VirtualKey Type Mapping', () => {
    it('should map SDK VirtualKeyDto to UI type correctly', () => {
      const sdkKey: VirtualKeyDto = {
        id: '123',
        keyName: 'Test Key',
        apiKey: 'sk_test_123',
        totalBudget: 100,
        budgetDuration: 'monthly',
        isEnabled: true,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
        usedBudget: 50,
        requestCount: 100,
        providers: ['openai', 'anthropic'],
      };

      const uiKey = mapVirtualKeyFromSDK(sdkKey);
      
      // Type assertions to ensure correct mapping
      expect(uiKey.name).toBe(sdkKey.keyName);
      expect(uiKey.key).toBe(sdkKey.apiKey);
      expect(uiKey.budgetPeriod).toBe(sdkKey.budgetDuration);
      expect(uiKey.isActive).toBe(sdkKey.isEnabled);
      
      // Ensure other properties are preserved
      expect(uiKey.id).toBe(sdkKey.id);
      expect(uiKey.totalBudget).toBe(sdkKey.totalBudget);
      expect(uiKey.createdAt).toBe(sdkKey.createdAt);
    });

    it('should map UI VirtualKey back to SDK type', () => {
      const uiKey: UIVirtualKey = {
        id: '123',
        name: 'Test Key',
        key: 'sk_test_123',
        totalBudget: 100,
        budgetPeriod: 'monthly',
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
        usedBudget: 50,
        requestCount: 100,
        providers: ['openai'],
      };

      const sdkKey = mapVirtualKeyToSDK(uiKey);
      
      expect(sdkKey.keyName).toBe(uiKey.name);
      expect(sdkKey.apiKey).toBe(uiKey.key);
      expect(sdkKey.budgetDuration).toBe(uiKey.budgetPeriod);
      expect(sdkKey.isEnabled).toBe(uiKey.isActive);
    });

    it('should handle optional fields correctly', () => {
      const minimalSdkKey: VirtualKeyDto = {
        id: '123',
        keyName: 'Minimal Key',
        apiKey: 'sk_minimal',
        isEnabled: true,
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        providers: [],
      };

      const uiKey = mapVirtualKeyFromSDK(minimalSdkKey);
      
      expect(uiKey.name).toBe('Minimal Key');
      expect(uiKey.totalBudget).toBeUndefined();
      expect(uiKey.budgetPeriod).toBeUndefined();
    });
  });

  describe('Provider Type Mapping', () => {
    it('should map SDK ProviderDto to UI type correctly', () => {
      const sdkProvider: ProviderDto = {
        id: 'openai',
        providerName: 'OpenAI',
        displayName: 'OpenAI GPT',
        isEnabled: true,
        apiKeyConfigured: true,
        baseUrl: 'https://api.openai.com',
        supportedModels: ['gpt-4', 'gpt-3.5-turbo'],
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      };

      const uiProvider = mapProviderFromSDK(sdkProvider);
      
      expect(uiProvider.name).toBe(sdkProvider.providerName);
      expect(uiProvider.isActive).toBe(sdkProvider.isEnabled);
      expect(uiProvider.id).toBe(sdkProvider.id);
      expect(uiProvider.hasApiKey).toBe(sdkProvider.apiKeyConfigured);
    });

    it('should map UI Provider back to SDK type', () => {
      const uiProvider: UIProvider = {
        id: 'anthropic',
        name: 'Anthropic',
        displayName: 'Claude',
        isActive: true,
        hasApiKey: true,
        baseUrl: 'https://api.anthropic.com',
        supportedModels: ['claude-3-opus', 'claude-3-sonnet'],
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      };

      const sdkProvider = mapProviderToSDK(uiProvider);
      
      expect(sdkProvider.providerName).toBe(uiProvider.name);
      expect(sdkProvider.isEnabled).toBe(uiProvider.isActive);
      expect(sdkProvider.apiKeyConfigured).toBe(uiProvider.hasApiKey);
    });
  });

  describe('ModelMapping Type Mapping', () => {
    it('should map SDK ModelMappingDto to UI type correctly', () => {
      const sdkMapping: ModelMappingDto = {
        id: '123',
        modelMappingName: 'GPT-4 Mapping',
        sourceModel: 'gpt-4-turbo',
        targetProvider: 'openai',
        targetModel: 'gpt-4-1106-preview',
        isEnabled: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      };

      const uiMapping = mapModelMappingFromSDK(sdkMapping);
      
      expect(uiMapping.name).toBe(sdkMapping.modelMappingName);
      expect(uiMapping.isActive).toBe(sdkMapping.isEnabled);
      expect(uiMapping.sourceModel).toBe(sdkMapping.sourceModel);
      expect(uiMapping.targetModel).toBe(sdkMapping.targetModel);
    });

    it('should map UI ModelMapping back to SDK type', () => {
      const uiMapping: UIModelMapping = {
        id: '456',
        name: 'Claude Mapping',
        sourceModel: 'claude-3',
        targetProvider: 'anthropic',
        targetModel: 'claude-3-opus-20240229',
        isActive: false,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      };

      const sdkMapping = mapModelMappingToSDK(uiMapping);
      
      expect(sdkMapping.modelMappingName).toBe(uiMapping.name);
      expect(sdkMapping.isEnabled).toBe(uiMapping.isActive);
    });
  });

  describe('Type Constraints at Compile Time', () => {
    it('should enforce correct ChatCompletionRequest structure', () => {
      // This test passes if TypeScript compilation succeeds
      const validRequest: ChatCompletionRequest = {
        model: 'gpt-4',
        messages: [
          { role: 'system', content: 'You are a helpful assistant.' },
          { role: 'user', content: 'Hello!' },
        ],
        temperature: 0.7,
        max_tokens: 1000,
      };

      expect(validRequest.model).toBe('gpt-4');
      expect(validRequest.messages).toHaveLength(2);
      expect(validRequest.messages[0].role).toBe('system');
      
      // TypeScript should enforce that role is one of the allowed values
      // @ts-expect-error - Invalid role should cause TypeScript error
      const invalidRequest: ChatCompletionRequest = {
        model: 'gpt-4',
        messages: [{ role: 'invalid', content: 'test' }],
      };
    });

    it('should enforce Model type structure', () => {
      const model: Model = {
        id: 'gpt-4',
        name: 'GPT-4',
        provider: 'openai',
        context_window: 8192,
        max_tokens: 4096,
        supports_functions: true,
        supports_vision: false,
      };

      expect(model.id).toBe('gpt-4');
      expect(model.supports_functions).toBe(true);
      
      // TypeScript should enforce required fields
      // @ts-expect-error - Missing required field
      const invalidModel: Model = {
        id: 'test',
        // Missing required fields
      };
    });
  });

  describe('Complex Type Mapping Scenarios', () => {
    it('should handle arrays of mapped types', () => {
      const sdkKeys: VirtualKeyDto[] = [
        {
          id: '1',
          keyName: 'Key 1',
          apiKey: 'sk_1',
          isEnabled: true,
          isActive: true,
          createdAt: '2024-01-01T00:00:00Z',
          providers: [],
        },
        {
          id: '2',
          keyName: 'Key 2',
          apiKey: 'sk_2',
          isEnabled: false,
          isActive: false,
          createdAt: '2024-01-02T00:00:00Z',
          providers: ['openai'],
        },
      ];

      const uiKeys = sdkKeys.map(mapVirtualKeyFromSDK);
      
      expect(uiKeys).toHaveLength(2);
      expect(uiKeys[0].name).toBe('Key 1');
      expect(uiKeys[1].name).toBe('Key 2');
      expect(uiKeys[1].isActive).toBe(false);
    });

    it('should handle nested type structures', () => {
      const systemInfo: SystemInfoDto = {
        version: '1.0.0',
        environment: 'production',
        uptime: 86400,
        services: {
          database: { status: 'healthy', latency: 5 },
          cache: { status: 'healthy', latency: 1 },
          queue: { status: 'degraded', latency: 100 },
        },
      };

      // Ensure nested types are properly structured
      expect(systemInfo.services.database.status).toBe('healthy');
      expect(typeof systemInfo.services.cache.latency).toBe('number');
    });

    it('should handle union types correctly', () => {
      type Status = 'active' | 'inactive' | 'pending';
      
      interface StatusItem {
        id: string;
        status: Status;
      }

      const item: StatusItem = {
        id: '123',
        status: 'active',
      };

      expect(item.status).toBe('active');
      
      // TypeScript should prevent invalid status values
      // @ts-expect-error - Invalid status value
      const invalidItem: StatusItem = {
        id: '456',
        status: 'invalid',
      };
    });
  });

  describe('Type Guard Functions', () => {
    it('should have type guards for different error types', () => {
      const isVirtualKeyDto = (obj: any): obj is VirtualKeyDto => {
        return obj &&
          typeof obj.id === 'string' &&
          typeof obj.keyName === 'string' &&
          typeof obj.apiKey === 'string' &&
          typeof obj.isEnabled === 'boolean';
      };

      const validKey = {
        id: '123',
        keyName: 'Test',
        apiKey: 'sk_test',
        isEnabled: true,
        isActive: true,
        createdAt: '2024-01-01',
        providers: [],
      };

      const invalidKey = {
        id: 123, // Wrong type
        keyName: 'Test',
      };

      expect(isVirtualKeyDto(validKey)).toBe(true);
      expect(isVirtualKeyDto(invalidKey)).toBe(false);
    });
  });

  describe('Type Inference', () => {
    it('should correctly infer types from mapping functions', () => {
      const sdkKey: VirtualKeyDto = {
        id: '123',
        keyName: 'Test',
        apiKey: 'sk_test',
        isEnabled: true,
        isActive: true,
        createdAt: '2024-01-01',
        providers: [],
      };

      // TypeScript should infer the return type as UIVirtualKey
      const uiKey = mapVirtualKeyFromSDK(sdkKey);
      
      // These should work without type assertions
      const keyName: string = uiKey.name;
      const isActive: boolean = uiKey.isActive;
      
      expect(keyName).toBe('Test');
      expect(isActive).toBe(true);
    });
  });
});