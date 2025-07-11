/**
 * Circuit breaker pattern for preventing cascading failures
 */
export class CircuitBreaker {
  private failures: number = 0;
  private lastFailureTime: number = 0;
  private state: 'closed' | 'open' | 'half-open' = 'closed';
  
  constructor(
    private readonly threshold: number = 5,
    private readonly resetTimeout: number = 60000 // 1 minute
  ) {}
  
  /**
   * Records a success and potentially closes the circuit
   */
  recordSuccess(): void {
    this.failures = 0;
    this.state = 'closed';
  }
  
  /**
   * Records a failure and potentially opens the circuit
   */
  recordFailure(): void {
    this.failures++;
    this.lastFailureTime = Date.now();
    
    if (this.failures >= this.threshold) {
      this.state = 'open';
    }
  }
  
  /**
   * Checks if requests should be allowed
   */
  shouldAllowRequest(): boolean {
    if (this.state === 'closed') {
      return true;
    }
    
    if (this.state === 'open') {
      // Check if enough time has passed to try again
      if (Date.now() - this.lastFailureTime >= this.resetTimeout) {
        this.state = 'half-open';
        return true; // Allow one request to test
      }
      return false; // Circuit is open, reject requests
    }
    
    // Half-open state - allow the request
    return true;
  }
  
  /**
   * Gets the current circuit state
   */
  getState(): 'closed' | 'open' | 'half-open' {
    return this.state;
  }
  
  /**
   * Resets the circuit breaker
   */
  reset(): void {
    this.failures = 0;
    this.state = 'closed';
    this.lastFailureTime = 0;
  }
}