
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { transformSDKResponse } from '@/lib/utils/sdk-transforms';
import { parseQueryParams } from '@/lib/utils/route-helpers';

interface SettingItem {
  key: string;
  value: string;
  category?: string;
}

// Default settings if none exist
const DEFAULT_SETTINGS = {
  systemName: 'Conduit LLM Platform',
  description: 'Unified LLM API Gateway and Management Platform',
  enableLogging: true,
  logLevel: 'Information',
  maxConcurrentRequests: 100,
  requestTimeoutSeconds: 30,
  cacheTimeoutMinutes: 30,
  enableRateLimiting: false,
  maxRequestsPerMinute: 1000,
  rateLimitWindowSeconds: 60,
  enableIpFiltering: false,
  enableRequestValidation: true,
  maxFailedAttempts: 5,
  enablePerformanceTracking: true,
  enableHealthChecks: true,
  healthCheckIntervalMinutes: 5,
};

export const GET = withSDKAuth(
  async (request, context) => {
    try {
      const params = parseQueryParams(request);
      const category = params.get('category');
      
      // Get system settings
      const result = await withSDKErrorHandling(
        async () => {
          if (category) {
            // Get settings by category
            const categorySettings = await context.adminClient!.settings.getSettingsByCategory(category);
            // Convert array to object format
            const settingsObj: Record<string, unknown> = {};
            categorySettings.forEach((setting: SettingItem) => {
              settingsObj[setting.key] = setting.value;
            });
            return { [category]: settingsObj };
          }
          
          // Get all global settings
          const allSettings = await context.adminClient!.settings.getGlobalSettings();
          // Convert array to object format grouped by category
          const settingsObj: Record<string, Record<string, unknown>> = {};
          allSettings.forEach((setting: SettingItem) => {
            const cat = setting.category || 'general';
            if (!settingsObj[cat]) {
              settingsObj[cat] = {};
            }
            settingsObj[cat][setting.key] = setting.value;
          });
          return settingsObj;
        },
        'get system settings'
      );

      // Return defaults if no settings found
      if (!result || Object.keys(result).length === 0) {
        return transformSDKResponse(DEFAULT_SETTINGS, {
          meta: { isDefault: true }
        });
      }

      return transformSDKResponse(result);
    } catch (error: unknown) {
      // Handle 404 by returning defaults
      if ((error as Record<string, unknown>)?.statusCode === 404 || (error as Record<string, unknown>)?.type === 'NOT_FOUND') {
        return transformSDKResponse(DEFAULT_SETTINGS, {
          meta: { isDefault: true }
        });
      }
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

export const PUT = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Update system settings
      const result = await withSDKErrorHandling(
        async () => {
          const updatePromises = [];
          
          // If updating a specific category
          if (body.category && body.settings) {
            for (const [key, value] of Object.entries(body.settings)) {
              updatePromises.push(
                context.adminClient!.settings.updateGlobalSetting(key, {
                  value: String(value),
                  category: body.category
                })
              );
            }
          } else {
            // Update all settings
            for (const [key, value] of Object.entries(body)) {
              if (key !== 'category' && key !== 'settings') {
                updatePromises.push(
                  context.adminClient!.settings.updateGlobalSetting(key, {
                    value: String(value)
                  })
                );
              }
            }
          }
          
          await Promise.all(updatePromises);
          return { success: true, updated: updatePromises.length };
        },
        'update system settings'
      );

      return transformSDKResponse(result, {
        meta: {
          updated: true,
          timestamp: new Date().toISOString(),
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Additional endpoint for specific setting categories
export const POST = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Create or update a specific setting
      const result = await withSDKErrorHandling(
        async () => {
          // Use setSetting method which is available in the SDK
          await context.adminClient!.settings.setSetting(
            body.key,
            String(body.value),
            {
              description: body.description,
              dataType: body.dataType || 'string',
              category: body.category,
              isSecret: body.isSecret
            }
          );
          
          return {
            key: body.key,
            value: body.value,
            category: body.category,
            updated: true
          };
        },
        'set system setting'
      );

      return transformSDKResponse(result, {
        status: 201,
        meta: {
          created: true,
          key: body.key,
        }
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);