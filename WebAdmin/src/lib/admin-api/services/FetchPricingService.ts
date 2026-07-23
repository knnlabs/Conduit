import type { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import type { RequestConfig } from '../client/types';
import type { components, paths } from '../generated/admin-api';
import { HttpMethod } from '../client/HttpMethod';
import type { PricingRulesConfig, PricingType } from '../models/pricing';

type ValidationResponse = components['schemas']['PricingValidationResponse'];
type SimulationRequest = components['schemas']['PricingSimulationRequest'];
type SimulationResponse = components['schemas']['PricingSimulationResponse'];
type AuditRequest = NonNullable<paths['/v1/admin/pricing-tools/audit/events']['get']['parameters']['query']>;
type AuditResponse = components['schemas']['PagedResultOfPricingAuditEventDto'];
type AuditSummary = components['schemas']['PricingAuditSummary'];

export class FetchPricingService {
  constructor(private readonly client: FetchBaseApiClient) {}

  async validate(rules: PricingRulesConfig, config?: RequestConfig): Promise<ValidationResponse> {
    const body: components['schemas']['PricingValidationRequest'] = {
      pricingConfiguration: { ...rules },
    };
    return this.client['executeContractOperation']('/v1/admin/pricing-tools/validate', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/pricing-tools/validate', { ...options, body }), config, body);
  }

  async simulate(request: SimulationRequest, config?: RequestConfig): Promise<SimulationResponse> {
    return this.client['executeContractOperation']('/v1/admin/pricing-tools/simulate', HttpMethod.POST,
      (contractClient, options) => contractClient.POST('/v1/admin/pricing-tools/simulate', { ...options, body: request }), config, request);
  }

  async getTemplate(pricingType?: PricingType, config?: RequestConfig): Promise<unknown> {
    const query = { pricingType };
    const suffix = pricingType ? `?pricingType=${encodeURIComponent(pricingType)}` : '';
    return this.client['executeContractRead'](`/v1/admin/pricing-tools/template${suffix}`,
      (contractClient, options) => contractClient.GET('/v1/admin/pricing-tools/template', { ...options, params: { query } }), config);
  }

  async queryAudit(request: AuditRequest, config?: RequestConfig): Promise<AuditResponse> {
    const queryString = new URLSearchParams(
      Object.entries(request).filter(([, value]) => value !== undefined).map(([key, value]) => [key, String(value)]),
    ).toString();
    return this.client['executeContractRead'](`/v1/admin/pricing-tools/audit/events?${queryString}`,
      (contractClient, options) => contractClient.GET('/v1/admin/pricing-tools/audit/events', {
        ...options, params: { query: request },
      }), config);
  }

  async getAuditSummary(from: string, to: string, virtualKeyId?: number, config?: RequestConfig): Promise<AuditSummary> {
    const query = { from, to, virtualKeyId };
    const queryString = new URLSearchParams(Object.entries(query)
      .filter(([, value]) => value !== undefined).map(([key, value]) => [key, String(value)])).toString();
    return this.client['executeContractRead'](`/v1/admin/pricing-tools/audit/summary?${queryString}`,
      (contractClient, options) => contractClient.GET('/v1/admin/pricing-tools/audit/summary', { ...options, params: { query } }), config);
  }
}
