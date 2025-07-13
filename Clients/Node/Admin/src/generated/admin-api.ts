/**
 * This file is auto-generated based on the Conduit Admin API controllers
 * DO NOT MODIFY DIRECTLY
 * 
 * Generated from Admin API endpoints:
 * - VirtualKeysController
 * - DashboardController
 * - ModelProviderMappingController
 * - ProviderCredentialsController
 * - GlobalSettingsController
 */

export interface operations {
  // Virtual Keys operations
  VirtualKeys_GetAll: {
    parameters: {
      query?: {
        page?: number;
        pageSize?: number;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": components["schemas"]["VirtualKeyListResponseDto"];
        };
      };
      401: {
        content: {
          "application/json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  Dashboard_Metrics: {
    parameters: {};
    responses: {
      200: {
        content: {
          "application/json": {
            totalRequests?: number;
            totalCost?: number;
            activeVirtualKeys?: number;
            errorRate?: number;
            avgResponseTime?: number;
            topModels?: Array<{
              model?: string;
              requests?: number;
              cost?: number;
            }>;
            recentActivity?: Array<{
              timestamp?: string;
              action?: string;
              details?: string;
            }>;
          };
        };
      };
      401: {
        content: {
          "application/json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  Dashboard_GetTimeSeriesData: {
    parameters: {
      query?: {
        interval?: "day" | "week" | "month";
        days?: number;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": {
            data?: Array<{
              date?: string;
              requests?: number;
              cost?: number;
              errors?: number;
            }>;
          };
        };
      };
      401: {
        content: {
          "application/json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
  Dashboard_GetProviderMetrics: {
    parameters: {
      query?: {
        days?: number;
      };
    };
    responses: {
      200: {
        content: {
          "application/json": Array<{
            provider?: string;
            requests?: number;
            totalCost?: number;
            avgResponseTime?: number;
            errorRate?: number;
          }>;
        };
      };
      401: {
        content: {
          "application/json": components["schemas"]["ErrorResponse"];
        };
      };
    };
  };
}

export interface paths {
  "/api/virtualkeys": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["VirtualKeyDto"][];
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["CreateVirtualKeyRequestDto"];
        };
      };
      responses: {
        201: {
          content: {
            "application/json": components["schemas"]["CreateVirtualKeyResponseDto"];
          };
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "application/json": {
              message: string;
            };
          };
        };
      };
    };
  };
  "/api/virtualkeys/{id}": {
    get: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["VirtualKeyDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    put: {
      parameters: {
        path: {
          id: number;
        };
      };
      requestBody: {
        content: {
          "application/json": components["schemas"]["UpdateVirtualKeyRequestDto"];
        };
      };
      responses: {
        204: {
          content: never;
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    delete: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        204: {
          content: never;
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/virtualkeys/{id}/reset-spend": {
    post: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        204: {
          content: never;
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/virtualkeys/validate": {
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["ValidateVirtualKeyRequest"];
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["VirtualKeyValidationResult"];
          };
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/virtualkeys/{id}/spend": {
    post: {
      parameters: {
        path: {
          id: number;
        };
      };
      requestBody: {
        content: {
          "application/json": components["schemas"]["UpdateSpendRequest"];
        };
      };
      responses: {
        204: {
          content: never;
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/virtualkeys/{id}/check-budget": {
    post: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["BudgetCheckResult"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/virtualkeys/{id}/validation-info": {
    get: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["VirtualKeyValidationInfoDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/virtualkeys/maintenance": {
    post: {
      responses: {
        204: {
          content: never;
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/dashboard/metrics/realtime": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": {
              timestamp: string;
              system: {
                totalRequestsHour: number;
                totalRequestsDay: number;
                avgLatencyHour: number;
                errorRateHour: number;
                activeProviders: number;
                activeKeys: number;
              };
              modelMetrics: Array<{
                model: string;
                requestCount: number;
                avgLatency: number;
                totalTokens: number;
                totalCost: number;
                errorRate: number;
              }>;
              providerStatus: Array<{
                providerName: string;
                isEnabled: boolean;
                lastHealthCheck?: {
                  isHealthy: boolean;
                  checkedAt: string;
                  responseTime: number;
                };
              }>;
              topKeys: Array<{
                id: number;
                name: string;
                requestsToday: number;
                costToday: number;
                budgetUtilization: number;
              }>;
              refreshIntervalSeconds: number;
            };
          };
        };
        500: {
          content: {
            "application/json": {
              error: string;
              message: string;
            };
          };
        };
      };
    };
  };
  "/api/dashboard/metrics/timeseries": {
    get: {
      parameters: {
        query?: {
          period?: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": {
              period: string;
              startTime: string;
              endTime: string;
              intervalMinutes: number;
              series: Array<{
                timestamp: string;
                requests: number;
                avgLatency: number;
                errors: number;
                totalCost: number;
                totalTokens: number;
              }>;
            };
          };
        };
        400: {
          content: {
            "application/json": {
              error: string;
            };
          };
        };
        500: {
          content: {
            "application/json": {
              error: string;
              message: string;
            };
          };
        };
      };
    };
  };
  "/api/dashboard/metrics/providers": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": {
              timestamp: string;
              modelMetrics: Array<{
                model: string;
                metrics: {
                  totalRequests: number;
                  successfulRequests: number;
                  failedRequests: number;
                  avgLatency: number;
                  p95Latency: number;
                  totalCost: number;
                  totalTokens: number;
                };
              }>;
              healthHistory: Array<{
                provider: string;
                healthChecks: number;
                successRate: number;
                avgResponseTime: number;
                lastCheck: string;
              }>;
            };
          };
        };
        500: {
          content: {
            "application/json": {
              error: string;
              message: string;
            };
          };
        };
      };
    };
  };
  "/api/modelprovidermapping": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ModelProviderMappingDto"][];
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["ModelProviderMappingDto"];
        };
      };
      responses: {
        201: {
          content: {
            "application/json": components["schemas"]["ModelProviderMappingDto"];
          };
        };
        400: {
          content: {
            "text/plain": string;
          };
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/{id}": {
    get: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ModelProviderMappingDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    put: {
      parameters: {
        path: {
          id: number;
        };
      };
      requestBody: {
        content: {
          "application/json": components["schemas"]["ModelProviderMappingDto"];
        };
      };
      responses: {
        204: {
          content: never;
        };
        400: {
          content: {
            "text/plain": string;
          };
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    delete: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        204: {
          content: never;
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/by-model/{modelId}": {
    get: {
      parameters: {
        path: {
          modelId: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ModelProviderMappingDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/providers": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderDataDto"][];
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/bulk": {
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["BulkModelMappingRequest"];
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["BulkModelMappingResponse"];
          };
        };
        400: {
          content: {
            "text/plain": string;
          };
        };
        401: {
          content: {
            "text/plain": string;
          };
        };
        403: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "application/json": components["schemas"]["BulkModelMappingResponse"];
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/discover/provider/{providerName}": {
    get: {
      parameters: {
        path: {
          providerName: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["DiscoveredModel"][];
          };
        };
        400: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/discover/model/{providerName}/{modelId}": {
    get: {
      parameters: {
        path: {
          providerName: string;
          modelId: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["DiscoveredModel"];
          };
        };
        400: {
          content: {
            "text/plain": string;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/modelprovidermapping/discover/capability/{modelAlias}/{capability}": {
    get: {
      parameters: {
        path: {
          modelAlias: string;
          capability: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": boolean;
          };
        };
        400: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/providercredentials": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderCredentialDto"][];
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["CreateProviderCredentialDto"];
        };
      };
      responses: {
        201: {
          content: {
            "application/json": components["schemas"]["ProviderCredentialDto"];
          };
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/providercredentials/{id}": {
    get: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderCredentialDto"];
          };
        };
        404: {
          content: {
            "application/json": {
              error: string;
            };
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    put: {
      parameters: {
        path: {
          id: number;
        };
      };
      requestBody: {
        content: {
          "application/json": components["schemas"]["UpdateProviderCredentialDto"];
        };
      };
      responses: {
        204: {
          content: never;
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        404: {
          content: {
            "application/json": {
              error: string;
            };
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    delete: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        204: {
          content: never;
        };
        404: {
          content: {
            "application/json": {
              error: string;
            };
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/providercredentials/name/{providerName}": {
    get: {
      parameters: {
        path: {
          providerName: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderCredentialDto"];
          };
        };
        404: {
          content: {
            "application/json": {
              error: string;
            };
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/providercredentials/names": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderDataDto"][];
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/providercredentials/test/{id}": {
    post: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderConnectionTestResultDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/providercredentials/test": {
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["ProviderCredentialDto"];
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["ProviderConnectionTestResultDto"];
          };
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/globalsettings": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["GlobalSettingDto"][];
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    post: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["CreateGlobalSettingDto"];
        };
      };
      responses: {
        201: {
          content: {
            "application/json": components["schemas"]["GlobalSettingDto"];
          };
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/globalsettings/{id}": {
    get: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["GlobalSettingDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    put: {
      parameters: {
        path: {
          id: number;
        };
      };
      requestBody: {
        content: {
          "application/json": components["schemas"]["UpdateGlobalSettingDto"];
        };
      };
      responses: {
        204: {
          content: never;
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    delete: {
      parameters: {
        path: {
          id: number;
        };
      };
      responses: {
        204: {
          content: never;
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/globalsettings/by-key/{key}": {
    get: {
      parameters: {
        path: {
          key: string;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": components["schemas"]["GlobalSettingDto"];
          };
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
    delete: {
      parameters: {
        path: {
          key: string;
        };
      };
      responses: {
        204: {
          content: never;
        };
        404: {
          content: {
            "text/plain": string;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
  "/api/globalsettings/by-key": {
    put: {
      requestBody: {
        content: {
          "application/json": components["schemas"]["UpdateGlobalSettingByKeyDto"];
        };
      };
      responses: {
        204: {
          content: never;
        };
        400: {
          content: {
            "application/json": any;
          };
        };
        500: {
          content: {
            "text/plain": string;
          };
        };
      };
    };
  };
}

export interface components {
  schemas: {
    // Common DTOs
    ErrorResponse: {
      error?: string;
      message?: string;
      statusCode?: number;
      timestamp?: string;
    };
    VirtualKeyListResponseDto: {
      items: components["schemas"]["VirtualKeyDto"][];
      totalCount: number;
      page: number;
      pageSize: number;
      totalPages: number;
    };
    // Virtual Key DTOs
    VirtualKeyDto: {
      id: number;
      keyName: string;
      keyPrefix?: string;
      allowedModels?: string;
      maxBudget?: number;
      currentSpend: number;
      budgetDuration?: string;
      budgetStartDate?: string;
      isEnabled: boolean;
      expiresAt?: string;
      createdAt: string;
      updatedAt: string;
      metadata?: string;
      rateLimitRpm?: number;
      rateLimitRpd?: number;
      description?: string;
      // Compatibility properties
      name: string;
      isActive: boolean;
      usageLimit?: number;
      rateLimit?: number;
    };
    CreateVirtualKeyRequestDto: {
      keyName: string;
      allowedModels?: string;
      maxBudget?: number;
      budgetDuration?: string;
      expiresAt?: string;
      metadata?: string;
      rateLimitRpm?: number;
      rateLimitRpd?: number;
    };
    CreateVirtualKeyResponseDto: {
      virtualKey: string;
      keyInfo: components["schemas"]["VirtualKeyDto"];
    };
    UpdateVirtualKeyRequestDto: {
      keyName?: string;
      allowedModels?: string;
      maxBudget?: number;
      budgetDuration?: string;
      isEnabled?: boolean;
      expiresAt?: string;
      metadata?: string;
      rateLimitRpm?: number;
      rateLimitRpd?: number;
    };
    ValidateVirtualKeyRequest: {
      key: string;
      requestedModel?: string;
    };
    VirtualKeyValidationResult: {
      isValid: boolean;
      virtualKeyId?: number;
      keyName?: string;
      allowedModels?: string;
      maxBudget?: number;
      currentSpend: number;
      errorMessage?: string;
    };
    UpdateSpendRequest: {
      cost: number;
    };
    BudgetCheckResult: {
      wasReset: boolean;
      newBudgetStartDate?: string;
    };
    VirtualKeyValidationInfoDto: {
      id: number;
      keyName: string;
      allowedModels?: string;
      maxBudget?: number;
      currentSpend: number;
      budgetDuration?: string;
      budgetStartDate?: string;
      isEnabled: boolean;
      expiresAt?: string;
      rateLimitRpm?: number;
      rateLimitRpd?: number;
    };

    // Model Provider Mapping DTOs
    ModelProviderMappingDto: {
      id: number;
      modelId: string;
      providerModelId: string;
      providerId: string;
      providerName?: string;
      priority: number;
      isEnabled: boolean;
      capabilities?: string;
      maxContextLength?: number;
      supportsVision: boolean;
      supportsAudioTranscription: boolean;
      supportsTextToSpeech: boolean;
      supportsRealtimeAudio: boolean;
      supportsImageGeneration: boolean;
      supportsVideoGeneration: boolean;
      supportsEmbeddings: boolean;
      tokenizerType?: string;
      supportedVoices?: string;
      supportedLanguages?: string;
      supportedFormats?: string;
      isDefault: boolean;
      defaultCapabilityType?: string;
      createdAt: string;
      updatedAt: string;
      notes?: string;
    };
    BulkModelMappingRequest: {
      mappings: components["schemas"]["CreateModelProviderMappingDto"][];
      replaceExisting: boolean;
      validateProviderModels: boolean;
    };
    CreateModelProviderMappingDto: {
      modelId: string;
      providerModelId: string;
      providerId: string;
      priority: number;
      isEnabled: boolean;
      capabilities?: string;
      maxContextLength?: number;
      supportsVision: boolean;
      supportsAudioTranscription: boolean;
      supportsTextToSpeech: boolean;
      supportsRealtimeAudio: boolean;
      supportsImageGeneration: boolean;
      supportsVideoGeneration: boolean;
      tokenizerType?: string;
      supportedVoices?: string;
      supportedLanguages?: string;
      supportedFormats?: string;
      isDefault: boolean;
      defaultCapabilityType?: string;
      notes?: string;
    };
    BulkModelMappingResponse: {
      created: components["schemas"]["ModelProviderMappingDto"][];
      updated: components["schemas"]["ModelProviderMappingDto"][];
      failed: components["schemas"]["BulkMappingError"][];
      totalProcessed: number;
      successCount: number;
      failureCount: number;
      isSuccess: boolean;
    };
    BulkMappingError: {
      index: number;
      mapping: components["schemas"]["CreateModelProviderMappingDto"];
      errorMessage: string;
      details?: string;
      errorType: "Validation" | "Duplicate" | "ProviderModelNotFound" | "SystemError" | "ProviderNotFound";
    };
    DiscoveredModel: {
      modelId: string;
      provider: string;
      displayName?: string;
      capabilities: components["schemas"]["ModelCapabilities"];
      metadata?: Record<string, any>;
      lastVerified: string;
    };
    ModelCapabilities: {
      chat: boolean;
      chatStream: boolean;
      embeddings: boolean;
      imageGeneration: boolean;
      vision: boolean;
      videoGeneration: boolean;
      videoUnderstanding: boolean;
      functionCalling: boolean;
      toolUse: boolean;
      jsonMode: boolean;
      maxTokens?: number;
      maxOutputTokens?: number;
      supportedImageSizes?: string[];
      supportedVideoResolutions?: string[];
      maxVideoDurationSeconds?: number;
    };

    // Provider Credential DTOs
    ProviderCredentialDto: {
      id: number;
      providerName: string;
      apiBase: string;
      apiKey: string;
      isEnabled: boolean;
      organization?: string;
      modelEndpoint?: string;
      additionalConfig?: string;
      orgId?: string;
      projectId?: string;
      region?: string;
      endpointUrl?: string;
      deploymentName?: string;
      createdAt: string;
      updatedAt: string;
    };
    CreateProviderCredentialDto: {
      providerName: string;
      apiBase?: string;
      apiKey?: string;
      isEnabled: boolean;
      organization?: string;
      modelEndpoint?: string;
      additionalConfig?: string;
    };
    UpdateProviderCredentialDto: {
      id: number;
      apiBase?: string;
      apiKey?: string;
      isEnabled: boolean;
      organization?: string;
      modelEndpoint?: string;
      additionalConfig?: string;
    };
    ProviderDataDto: {
      id: number;
      providerName: string;
    };
    ProviderConnectionTestResultDto: {
      success: boolean;
      message: string;
      errorDetails?: string;
      providerName: string;
      timestamp: string;
    };

    // Global Settings DTOs
    GlobalSettingDto: {
      id: number;
      key: string;
      value: string;
      description?: string;
      createdAt: string;
      updatedAt: string;
    };
    CreateGlobalSettingDto: {
      key: string;
      value: string;
      description?: string;
    };
    UpdateGlobalSettingDto: {
      id: number;
      value: string;
      description?: string;
    };
    UpdateGlobalSettingByKeyDto: {
      key: string;
      value: string;
      description?: string;
    };
  };
}

// Export type aliases for convenience
export type VirtualKeyDto = components["schemas"]["VirtualKeyDto"];
export type CreateVirtualKeyRequestDto = components["schemas"]["CreateVirtualKeyRequestDto"];
export type CreateVirtualKeyResponseDto = components["schemas"]["CreateVirtualKeyResponseDto"];
export type UpdateVirtualKeyRequestDto = components["schemas"]["UpdateVirtualKeyRequestDto"];
export type ValidateVirtualKeyRequest = components["schemas"]["ValidateVirtualKeyRequest"];
export type VirtualKeyValidationResult = components["schemas"]["VirtualKeyValidationResult"];
export type UpdateSpendRequest = components["schemas"]["UpdateSpendRequest"];
export type BudgetCheckResult = components["schemas"]["BudgetCheckResult"];
export type VirtualKeyValidationInfoDto = components["schemas"]["VirtualKeyValidationInfoDto"];

export type ModelProviderMappingDto = components["schemas"]["ModelProviderMappingDto"];
export type BulkModelMappingRequest = components["schemas"]["BulkModelMappingRequest"];
export type CreateModelProviderMappingDto = components["schemas"]["CreateModelProviderMappingDto"];
export type BulkModelMappingResponse = components["schemas"]["BulkModelMappingResponse"];
export type BulkMappingError = components["schemas"]["BulkMappingError"];
export type DiscoveredModel = components["schemas"]["DiscoveredModel"];
export type ModelCapabilities = components["schemas"]["ModelCapabilities"];

export type ProviderCredentialDto = components["schemas"]["ProviderCredentialDto"];
export type CreateProviderCredentialDto = components["schemas"]["CreateProviderCredentialDto"];
export type UpdateProviderCredentialDto = components["schemas"]["UpdateProviderCredentialDto"];
export type ProviderDataDto = components["schemas"]["ProviderDataDto"];
export type ProviderConnectionTestResultDto = components["schemas"]["ProviderConnectionTestResultDto"];

export type GlobalSettingDto = components["schemas"]["GlobalSettingDto"];
export type CreateGlobalSettingDto = components["schemas"]["CreateGlobalSettingDto"];
export type UpdateGlobalSettingDto = components["schemas"]["UpdateGlobalSettingDto"];
export type UpdateGlobalSettingByKeyDto = components["schemas"]["UpdateGlobalSettingByKeyDto"];