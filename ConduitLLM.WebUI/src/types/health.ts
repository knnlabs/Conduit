// Import health check types from Admin SDK (WebUI uses Admin API for health checks)
export type {
  HealthStatusDto,
  HealthCheckDetail,
  HealthCheckData
} from '@knn_labs/conduit-admin-client';

// Also import Core SDK health types for completeness
export type {
  HealthCheckResponse,
  HealthCheckItem,
  HealthSummary,
  HealthStatus,
  HealthCheckOptions,
  SimpleHealthStatus,
  WaitForHealthOptions
} from '@knn_labs/conduit-core-client';