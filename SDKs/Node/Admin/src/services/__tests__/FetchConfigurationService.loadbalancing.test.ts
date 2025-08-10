import { createMockClient, type MockClient } from '../../__tests__/helpers/mockClient.helper';
import { FetchConfigurationService } from '../FetchConfigurationService';
import { ENDPOINTS } from '../../constants';
import type {
  LoadBalancerConfigDto,
  LoadBalancerHealthDto,
} from '../../models/configurationExtended';

describe('FetchConfigurationService - Load Balancing', () => {
  let service: FetchConfigurationService;
  let mockClient: MockClient;

  beforeEach(() => {
    mockClient = createMockClient();
    service = new FetchConfigurationService(mockClient as any);
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  describe('Load Balancing', () => {
    it('should get load balancer config', async () => {
      const mockConfig: LoadBalancerConfigDto = {
        algorithm: 'weighted_round_robin',
        healthCheck: {
          enabled: true,
          intervalSeconds: 30,
          timeoutSeconds: 5,
          unhealthyThreshold: 3,
          healthyThreshold: 2
        },
        weights: {
          'openai': 70,
          'anthropic': 30
        }
      };

      mockClient.get = jest.fn().mockResolvedValue(mockConfig);

      const result = await service.getLoadBalancerConfig();

      expect(mockClient.get).toHaveBeenCalledWith(
        ENDPOINTS.CONFIGURATION.LOAD_BALANCER,
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockConfig);
    });

    it('should get load balancer health', async () => {
      const mockHealth: LoadBalancerHealthDto = {
        status: 'healthy',
        nodes: [
          {
            id: 'node-1',
            endpoint: 'https://api.openai.com',
            status: 'healthy',
            weight: 70,
            activeConnections: 45,
            totalRequests: 10000,
            avgResponseTime: 250,
            lastHealthCheck: '2024-01-01T00:00:00Z'
          }
        ],
        lastCheck: '2024-01-01T00:00:00Z',
        distribution: {
          'openai': 7000,
          'anthropic': 3000
        }
      };

      mockClient.get = jest.fn().mockResolvedValue(mockHealth);

      const result = await service.getLoadBalancerHealth();

      expect(mockClient.get).toHaveBeenCalledWith(
        '/api/config/loadbalancer/health',
        { signal: undefined, timeout: undefined, headers: undefined }
      );
      expect(result).toEqual(mockHealth);
    });
  });

  describe('recommendLoadBalancerAlgorithm', () => {
    it('should recommend round robin for similar performance', () => {
      const nodes: LoadBalancerHealthDto['nodes'] = [
        { id: '1', endpoint: 'url1', status: 'healthy', weight: 50, activeConnections: 10, totalRequests: 1000, avgResponseTime: 100, lastHealthCheck: '' },
        { id: '2', endpoint: 'url2', status: 'healthy', weight: 50, activeConnections: 12, totalRequests: 1000, avgResponseTime: 105, lastHealthCheck: '' }
      ];

      const recommendation = service.recommendLoadBalancerAlgorithm(nodes);
      expect(recommendation).toBe('round_robin');
    });

    it('should recommend least connections for high load', () => {
      const nodes: LoadBalancerHealthDto['nodes'] = [
        { id: '1', endpoint: 'url1', status: 'healthy', weight: 50, activeConnections: 150, totalRequests: 10000, avgResponseTime: 200, lastHealthCheck: '' },
        { id: '2', endpoint: 'url2', status: 'healthy', weight: 50, activeConnections: 50, totalRequests: 10000, avgResponseTime: 100, lastHealthCheck: '' }
      ];

      const recommendation = service.recommendLoadBalancerAlgorithm(nodes);
      expect(recommendation).toBe('least_connections');
    });
  });
});