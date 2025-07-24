// Health check types for Core API responses
export interface CoreHealthCheckResponse {
  status: string;
  checks: CoreHealthCheck[];
}

export interface CoreHealthCheck {
  name: string;
  status: string;
  description?: string;
  data?: Record<string, unknown>;
}