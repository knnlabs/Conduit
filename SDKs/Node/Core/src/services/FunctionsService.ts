import { BaseService } from './BaseService';
import type {
  FunctionExecutionRequest,
  FunctionExecutionResponse,
  FunctionExecution,
} from '../models/functions';

/**
 * Service for executing functions (e.g., Exa search) through the Core API
 */
export class FunctionsService extends BaseService {
  /**
   * Execute a function with the provided parameters
   * @param request - The function execution request
   * @returns The function execution result
   */
  async execute(request: FunctionExecutionRequest): Promise<FunctionExecutionResponse> {
    return this.clientAdapter.post<FunctionExecutionResponse>('/v1/functions/execute', request);
  }

  /**
   * Get the status and result of a function execution
   * @param executionId - The execution ID
   * @returns The function execution details
   */
  async getExecution(executionId: string): Promise<FunctionExecution> {
    return this.clientAdapter.get<FunctionExecution>(`/v1/functions/executions/${executionId}`);
  }
}
