// API Hooks for SDK integration
// These hooks provide a clean interface for components to interact with the backend
// through the WebUI's API routes, which in turn use the SDK clients.

export { useAuthApi } from '../useAuthApi';
export { useCoreApi } from '../useCoreApi';
export { useSecurityApi } from '../useSecurityApi';
export { useConfigurationApi } from '../useConfigurationApi';
export { useExportApi } from '../useExportApi';
export { useMonitoringApi } from '../useMonitoringApi';
export { useSystemApi } from '../useSystemApi';
export { useProviderApi } from '../useProviderApi';
export { useBackendHealth } from '../useBackendHealth';

// Re-export types
export type { ExportFormat, ExportType } from '../useExportApi';
export type { BackendHealthStatus } from '../useBackendHealth';