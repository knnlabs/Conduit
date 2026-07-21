import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import { PagedResult } from '../models/common-types';
import {
  PricingRulesConfig,
  PricingValidationResult,
  PricingSimulationRequest,
  PricingSimulationResult,
  PricingAuditEvent,
  PricingAuditQueryParams,
  PricingAuditSummary,
  PricingTemplate,
} from '../models/pricing';

/**
 * Service for pricing rules validation, simulation, and audit
 */
export class FetchPricingService {
  private readonly basePath = '/api/pricing';

  constructor(private readonly client: FetchBaseApiClient) {}

  /**
   * Validate a pricing configuration
   */
  async validate(
    config: PricingRulesConfig,
    requestConfig?: RequestConfig
  ): Promise<PricingValidationResult> {
    return this.client['post']<PricingValidationResult, PricingRulesConfig>(
      `${this.basePath}/validate`,
      config,
      {
        signal: requestConfig?.signal,
        timeout: requestConfig?.timeout,
        headers: requestConfig?.headers,
      }
    );
  }

  /**
   * Validate pricing configuration from JSON string
   */
  async validateJson(
    json: string,
    requestConfig?: RequestConfig
  ): Promise<PricingValidationResult> {
    return this.client['post']<PricingValidationResult, { json: string }>(
      `${this.basePath}/validate`,
      { json },
      {
        signal: requestConfig?.signal,
        timeout: requestConfig?.timeout,
        headers: requestConfig?.headers,
      }
    );
  }

  /**
   * Simulate pricing calculation with test parameters
   */
  async simulate(
    request: PricingSimulationRequest,
    requestConfig?: RequestConfig
  ): Promise<PricingSimulationResult> {
    return this.client['post']<PricingSimulationResult, PricingSimulationRequest>(
      `${this.basePath}/simulate`,
      request,
      {
        signal: requestConfig?.signal,
        timeout: requestConfig?.timeout,
        headers: requestConfig?.headers,
      }
    );
  }

  /**
   * Get pricing configuration templates
   */
  async getTemplates(
    requestConfig?: RequestConfig
  ): Promise<PricingTemplate[]> {
    return this.client['get']<PricingTemplate[]>(
      `${this.basePath}/template`,
      {
        signal: requestConfig?.signal,
        timeout: requestConfig?.timeout,
        headers: requestConfig?.headers,
      }
    );
  }

  /**
   * Query pricing audit events
   */
  async queryAudit(
    params: PricingAuditQueryParams,
    requestConfig?: RequestConfig
  ): Promise<PagedResult<PricingAuditEvent>> {
    const queryParams = new URLSearchParams();

    if (params.virtualKeyId !== undefined) {
      queryParams.append('virtualKeyId', String(params.virtualKeyId));
    }
    if (params.modelId) {
      queryParams.append('modelId', params.modelId);
    }
    if (params.modelCostId !== undefined) {
      queryParams.append('modelCostId', String(params.modelCostId));
    }
    if (params.startDate) {
      queryParams.append('startDate', params.startDate);
    }
    if (params.endDate) {
      queryParams.append('endDate', params.endDate);
    }
    if (params.page !== undefined) {
      queryParams.append('page', String(params.page));
    }
    if (params.pageSize !== undefined) {
      queryParams.append('pageSize', String(params.pageSize));
    }

    const url = queryParams.toString()
      ? `${this.basePath}/audit/query?${queryParams.toString()}`
      : `${this.basePath}/audit/query`;

    return this.client['post']<PagedResult<PricingAuditEvent>, PricingAuditQueryParams>(
      url,
      params,
      {
        signal: requestConfig?.signal,
        timeout: requestConfig?.timeout,
        headers: requestConfig?.headers,
      }
    );
  }

  /**
   * Get pricing audit summary statistics
   */
  async getAuditSummary(
    startDate?: string,
    endDate?: string,
    modelCostId?: number,
    requestConfig?: RequestConfig
  ): Promise<PricingAuditSummary> {
    const queryParams = new URLSearchParams();

    if (startDate) {
      queryParams.append('startDate', startDate);
    }
    if (endDate) {
      queryParams.append('endDate', endDate);
    }
    if (modelCostId !== undefined) {
      queryParams.append('modelCostId', String(modelCostId));
    }

    const url = queryParams.toString()
      ? `${this.basePath}/audit/summary?${queryParams.toString()}`
      : `${this.basePath}/audit/summary`;

    return this.client['get']<PricingAuditSummary>(url, {
      signal: requestConfig?.signal,
      timeout: requestConfig?.timeout,
      headers: requestConfig?.headers,
    });
  }
}
