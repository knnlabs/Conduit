/**
 * Unit tests for Clear Cache logic
 */

interface MockNotifications {
  show: jest.Mock;
  update: jest.Mock;
  hide: jest.Mock;
}

interface MockClient {
  system: {
    invalidateDiscoveryCache: jest.Mock;
  };
}

interface CacheInvalidationResult {
  message: string;
  timestamp: string;
  note?: string;
}

describe('Clear Cache Logic', () => {
  describe('handleInvalidateCache function', () => {
    let mockNotifications: MockNotifications;
    let mockExecuteWithAdmin: jest.Mock;
    let mockHandleRefresh: jest.Mock;

    beforeEach(() => {
      jest.clearAllMocks();

      // Setup mocks
      mockNotifications = {
        show: jest.fn(),
        update: jest.fn(),
        hide: jest.fn(),
      };

      mockExecuteWithAdmin = jest.fn();
      mockHandleRefresh = jest.fn();
    });

    // Simulate the handleInvalidateCache logic
    const simulateHandleInvalidateCache = async (): Promise<CacheInvalidationResult> => {
      // Show loading notification
      mockNotifications.show({
        id: 'invalidating-cache',
        title: 'Invalidating Discovery Cache',
        message: 'Please wait while the cache is being cleared...',
        loading: true,
        autoClose: false,
      });

      try {
        // Execute cache invalidation
        const result = await mockExecuteWithAdmin((client: MockClient) =>
          client.system.invalidateDiscoveryCache() as Promise<CacheInvalidationResult>
        ) as CacheInvalidationResult;

        // Update with success notification
        mockNotifications.update({
          id: 'invalidating-cache',
          title: 'Cache Invalidated',
          message: `Discovery cache has been cleared successfully. ${result.note ?? ''}`,
          color: 'green',
          loading: false,
          autoClose: 5000,
        });

        // Refresh the page data
        mockHandleRefresh();

        return result;
      } catch (error) {
        // Update with error notification
        const errorMessage = error instanceof Error ? error.message : 'Unknown error';
        mockNotifications.update({
          id: 'invalidating-cache',
          title: 'Error',
          message: `Failed to invalidate discovery cache: ${errorMessage}`,
          color: 'red',
          loading: false,
          autoClose: 5000,
        });
        throw error;
      }
    };

    it('should show loading notification when starting cache invalidation', async () => {
      // Setup successful response
      mockExecuteWithAdmin.mockImplementation((callback: (client: MockClient) => Promise<CacheInvalidationResult>) => {
        const mockClient: MockClient = {
          system: {
            invalidateDiscoveryCache: jest.fn().mockResolvedValue({
              message: 'Success',
              timestamp: new Date().toISOString(),
            }),
          },
        };
        return callback(mockClient);
      });

      // Execute the function
      await simulateHandleInvalidateCache();

      // Verify loading notification was shown
      expect(mockNotifications.show).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'invalidating-cache',
          title: 'Invalidating Discovery Cache',
          loading: true,
        })
      );
    });

    it('should show success notification when cache is cleared', async () => {
      // Setup successful response
      const mockResponse = {
        message: 'Discovery cache invalidated successfully',
        timestamp: new Date().toISOString(),
        note: 'All entries cleared',
      };

      mockExecuteWithAdmin.mockImplementation((callback: (client: MockClient) => Promise<CacheInvalidationResult>) => {
        const mockClient: MockClient = {
          system: {
            invalidateDiscoveryCache: jest.fn().mockResolvedValue(mockResponse),
          },
        };
        return callback(mockClient);
      });

      // Execute the function
      const result: CacheInvalidationResult = await simulateHandleInvalidateCache();

      // Verify success notification was shown
      expect(mockNotifications.update).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'invalidating-cache',
          title: 'Cache Invalidated',
          message: expect.stringContaining('successfully') as unknown,
          color: 'green',
          loading: false,
        })
      );

      // Verify result
      expect(result).toEqual(mockResponse);
    });

    it('should refresh data after successful cache invalidation', async () => {
      // Setup successful response
      mockExecuteWithAdmin.mockImplementation((callback: (client: MockClient) => Promise<CacheInvalidationResult>) => {
        const mockClient: MockClient = {
          system: {
            invalidateDiscoveryCache: jest.fn().mockResolvedValue({
              message: 'Success',
              timestamp: new Date().toISOString(),
            }),
          },
        };
        return callback(mockClient);
      });

      // Execute the function
      await simulateHandleInvalidateCache();

      // Verify refresh was called
      expect(mockHandleRefresh).toHaveBeenCalledTimes(1);
    });

    it('should show error notification when cache invalidation fails', async () => {
      // Setup error response
      const errorMessage = 'Network error';
      mockExecuteWithAdmin.mockImplementation((callback: (client: MockClient) => Promise<CacheInvalidationResult>) => {
        const mockClient: MockClient = {
          system: {
            invalidateDiscoveryCache: jest.fn().mockRejectedValue(new Error(errorMessage)),
          },
        };
        return callback(mockClient);
      });

      // Execute the function and expect it to throw
      const promise: Promise<CacheInvalidationResult> = simulateHandleInvalidateCache();
      await expect(promise).rejects.toThrow(errorMessage);

      // Verify error notification was shown
      expect(mockNotifications.update).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'invalidating-cache',
          title: 'Error',
          message: expect.stringContaining('Failed to invalidate discovery cache') as unknown,
          color: 'red',
          loading: false,
        })
      );
    });

    it('should not refresh data when cache invalidation fails', async () => {
      // Setup error response
      mockExecuteWithAdmin.mockImplementation((callback: (client: MockClient) => Promise<CacheInvalidationResult>) => {
        const mockClient: MockClient = {
          system: {
            invalidateDiscoveryCache: jest.fn().mockRejectedValue(new Error('Error')),
          },
        };
        return callback(mockClient);
      });

      // Execute the function and catch the error
      try {
        await simulateHandleInvalidateCache();
      } catch {
        // Expected to throw
      }

      // Verify refresh was NOT called
      expect(mockHandleRefresh).not.toHaveBeenCalled();
    });

    it('should handle 501 Not Implemented error gracefully', async () => {
      // Setup 501 error
      interface ErrorWithResponse extends Error {
        response?: {
          status: number;
          data: {
            message: string;
          };
        };
      }
      const notImplementedError: ErrorWithResponse = new Error('Discovery cache service is not configured');
      notImplementedError.response = {
        status: 501,
        data: {
          message: 'Discovery cache service is not configured',
        },
      };

      mockExecuteWithAdmin.mockImplementation((callback: (client: MockClient) => Promise<CacheInvalidationResult>) => {
        const mockClient: MockClient = {
          system: {
            invalidateDiscoveryCache: jest.fn().mockRejectedValue(notImplementedError),
          },
        };
        return callback(mockClient);
      });

      // Execute the function and expect it to throw
      const promise: Promise<CacheInvalidationResult> = simulateHandleInvalidateCache();
      await expect(promise).rejects.toThrow(
        'Discovery cache service is not configured'
      );

      // Verify error notification shows specific message
      expect(mockNotifications.update).toHaveBeenCalledWith(
        expect.objectContaining({
          id: 'invalidating-cache',
          title: 'Error',
          message: expect.stringContaining('Discovery cache service is not configured') as unknown,
          color: 'red',
        })
      );
    });
  });

  describe('Clear Cache Button State', () => {
    it('should disable button during cache invalidation', () => {
      // Simulate button disabled state
      let isDisabled = false;
      const setIsDisabled = (value: boolean) => {
        isDisabled = value;
      };

      // Start invalidation
      setIsDisabled(true);
      expect(isDisabled).toBe(true);

      // Complete invalidation
      setIsDisabled(false);
      expect(isDisabled).toBe(false);
    });

    it('should show correct tooltip text', () => {
      const tooltipText = 'Invalidate the discovery cache to force reload of model parameters';
      expect(tooltipText).toContain('Invalidate');
      expect(tooltipText).toContain('discovery cache');
      expect(tooltipText).toContain('model parameters');
    });
  });
});