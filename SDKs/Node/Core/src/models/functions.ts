/**
 * Function execution request
 */
export interface FunctionExecutionRequest {
  /**
   * The function configuration ID to execute
   */
  functionConfigurationId: number;

  /**
   * Parameters to pass to the function
   */
  parameters?: Record<string, unknown>;

  /**
   * Optional metadata to associate with the execution
   */
  metadata?: Record<string, unknown>;

  /**
   * Optional idempotency key to prevent duplicate executions
   */
  idempotencyKey?: string;
}

/**
 * Function execution response
 */
export interface FunctionExecutionResponse {
  /**
   * The unique execution ID
   */
  executionId: string;

  /**
   * The function configuration ID that was executed
   */
  functionConfigurationId: number;

  /**
   * The current state of the execution
   */
  state: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

  /**
   * The function execution result (provider-specific)
   */
  result?: Record<string, unknown>;

  /**
   * Error message if execution failed
   */
  errorMessage?: string;

  /**
   * Estimated cost before execution
   */
  estimatedCost?: number;

  /**
   * Actual cost after execution
   */
  actualCost?: number;

  /**
   * When the execution started
   */
  startedAt?: string;

  /**
   * When the execution completed
   */
  completedAt?: string;

  /**
   * Execution duration in milliseconds
   */
  duration?: number;
}

/**
 * Get execution by ID response
 */
export interface FunctionExecution extends FunctionExecutionResponse {}
