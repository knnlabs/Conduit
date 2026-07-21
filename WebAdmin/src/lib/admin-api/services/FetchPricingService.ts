import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { components } from '../generated/admin-api';
import { HttpMethod } from '../client/HttpMethod';
import type { PricingRulesConfig, PricingType } from '../models/pricing';

type ValidationResponse = components['schemas']['PricingValidationResponse'];
type SimulationRequest = components['schemas']['PricingSimulationRequest'];
type SimulationResponse = components['schemas']['PricingSimulationResponse'];
type AuditRequest = components['schemas']['PricingAuditQueryRequest'];
type AuditResponse = components['schemas']['PricingAuditQueryResponse'];
type AuditSummary = components['schemas']['PricingAuditSummary'];

export class FetchPricingService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async validate(rules: PricingRulesConfig, config?: RequestConfig): Promise<ValidationResponse> {
    const body = { pricingConfiguration: JSON.stringify(rules) };
    return this.client['executeContractOperation']('/api/Pricing/validate', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Pricing/validate', { ...options, body }), config, body);
  }

  async simulate(request: SimulationRequest, config?: RequestConfig): Promise<SimulationResponse> {
    return this.client['executeContractOperation']('/api/Pricing/simulate', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Pricing/simulate', { ...options, body: request }), config, request);
  }

  async getTemplate(pricingType?: PricingType, config?: RequestConfig): Promise<unknown> {
    const query = { pricingType };
    const suffix = pricingType ? `?pricingType=${encodeURIComponent(pricingType)}` : '';
    return this.client['executeContractRead'](`/api/Pricing/template${suffix}`,
      (contractClient, options) => contractClient.GET('/api/Pricing/template', { ...options, params: { query } }), config);
  }

  async queryAudit(request: AuditRequest & { from: string; to: string }, config?: RequestConfig): Promise<AuditResponse> {
    return this.client['executeContractOperation']('/api/Pricing/audit/query', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/api/Pricing/audit/query', { ...options, body: request }), config, request);
  }

  async getAuditSummary(from: string, to: string, virtualKeyId?: number, config?: RequestConfig): Promise<AuditSummary> {
    const query = { from, to, virtualKeyId };
    const queryString = new URLSearchParams(Object.entries(query)
      .filter(([, value]) => value !== undefined).map(([key, value]) => [key, String(value)])).toString();
    return this.client['executeContractRead'](`/api/Pricing/audit/summary?${queryString}`,
      (contractClient, options) => contractClient.GET('/api/Pricing/audit/summary', { ...options, params: { query } }), config);
  }
}
