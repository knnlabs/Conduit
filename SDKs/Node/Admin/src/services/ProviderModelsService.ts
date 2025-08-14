import { FetchBaseApiClient } from '../client/FetchBaseApiClient';

export interface ProviderModel {
  id: string;
  object: 'model';
  created: number;
  owned_by: string;
}

export interface ProviderModelsResponse {
  object: 'list';
  data: ProviderModel[];
}

/**
 * Service for managing provider models
 * NOTE: This service has been deprecated. Provider models are now managed through the ModelProviderMapping endpoints.
 */
export class ProviderModelsService extends FetchBaseApiClient {
  // This service is deprecated - all methods removed
}