import type { FetchBasedClient } from '../client/FetchBasedClient';
import { createClientAdapter, type IFetchBasedClientAdapter } from '../client/ClientAdapter';

/**
 * Base service class that provides common functionality for all services.
 * This class establishes the pattern for service construction and client access.
 */
export abstract class BaseService {
  protected readonly clientAdapter: IFetchBasedClientAdapter;

  constructor(protected readonly client: FetchBasedClient) {
    this.clientAdapter = createClientAdapter(client);
  }
}