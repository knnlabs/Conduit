import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchConfigurationService } from '../FetchConfigurationService';
import { ENDPOINTS } from '../../constants';
import type {
  FeatureFlag,
  UpdateFeatureFlagDto,
} from '../../models/configurationExtended';

describe('FetchConfigurationService - Feature Flags', () => {
  let service: FetchConfigurationService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchConfigurationService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Feature Flags', () => {
    it('should get feature flags', async () => {
      const mockFlags: FeatureFlag[] = [
        {
          key: 'new-ui',
          name: 'New UI Experience',
          description: 'Enable new dashboard UI',
          enabled: true,
          rolloutPercentage: 50,
          lastModified: '2024-01-01T00:00:00Z'
        }
      ];

      mockClient.get = jest.fn().mockResolvedValue(mockFlags);

      const result = await service.getFeatureFlags();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.FEATURES,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockFlags);
    });

    it('should update feature flag', async () => {
      const updateData: UpdateFeatureFlagDto = {
        enabled: true,
        rolloutPercentage: 75
      };

      const mockFlag: FeatureFlag = {
        key: 'new-ui',
        name: 'New UI Experience',
        enabled: true,
        rolloutPercentage: 75,
        lastModified: '2024-01-01T00:00:00Z'
      };

      mockClient.put = jest.fn().mockResolvedValue(mockFlag);

      const result = await service.updateFeatureFlag('new-ui', updateData);

      expect(mockClient.put).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.FEATURE_BY_KEY('new-ui'),
        updateData,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockFlag);
    });
  });

  describe('evaluateFeatureFlag', () => {
    it('should evaluate feature flag with rollout percentage', () => {
      const flag: FeatureFlag = {
        key: 'test-feature',
        name: 'Test Feature',
        enabled: true,
        rolloutPercentage: 50,
        lastModified: '2024-01-01'
      };

      // Test multiple times to check distribution
      let enabledCount = 0;
      for (let i = 0; i < 100; i++) {
        const result = service.evaluateFeatureFlag(flag, { userId: `user-${i}` });
        if (result) enabledCount++;
      }

      // Should be roughly 50% enabled
      expect(enabledCount).toBeGreaterThan(30);
      expect(enabledCount).toBeLessThan(70);
    });

    it('should evaluate feature flag with conditions', () => {
      const flag: FeatureFlag = {
        key: 'premium-feature',
        name: 'Premium Feature',
        enabled: true,
        conditions: [
          { type: 'user', field: 'plan', operator: 'equals', values: ['premium'] }
        ],
        lastModified: '2024-01-01'
      };

      expect(service.evaluateFeatureFlag(flag, { plan: 'premium' })).toBe(true);
      expect(service.evaluateFeatureFlag(flag, { plan: 'free' })).toBe(false);
    });
  });
});