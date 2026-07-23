import type { paths as AdminPaths } from '@/generated/admin-api';
import type { paths as GatewayPaths } from '@/generated/gateway-api';

export const ADMIN_CONTRACT_ROUTES = {
  ephemeralMasterKey: '/api/admin/auth/ephemeral-master-key',
  virtualKeys: '/api/VirtualKeys',
  virtualKey: '/api/VirtualKeys/{id}',
  validateVirtualKey: '/api/VirtualKeys/validate',
  virtualKeyGroups: '/api/VirtualKeyGroups',
  virtualKeyGroup: '/api/VirtualKeyGroups/{id}',
  adjustVirtualKeyGroupBalance: '/api/VirtualKeyGroups/{id}/adjust-balance',
  refundVirtualKeyGroup: '/api/VirtualKeyGroups/{id}/refund',
  analyticsExport: '/api/Analytics/export',
} as const satisfies Record<string, keyof AdminPaths>;

export const GATEWAY_CONTRACT_ROUTES = {
  ephemeralKey: '/v1/conduit/auth/ephemeral-key',
  chatCompletions: '/v1/chat/completions',
  discoveryModels: '/v1/conduit/discovery/models',
  functionParameters: '/v1/conduit/discovery/functions/{functionConfigurationId}/parameters',
  executeFunction: '/v1/conduit/functions/execute',
  imageGenerations: '/v1/images/generations',
  mediaUpload: '/v1/conduit/media/upload',
  createVideoTask: '/v1/conduit/videos/generations/async',
  videoTaskStatus: '/v1/conduit/videos/generations/tasks/{taskId}',
  cancelVideoTask: '/v1/conduit/videos/generations/{taskId}',
} as const satisfies Record<string, keyof GatewayPaths>;

export function materializeContractPath(
  template: string,
  parameters: Record<string, string | number>,
): string {
  return template.replace(/\{([^}]+)\}/g, (wholeMatch: string, name: string) => {
    void wholeMatch;
    const value = parameters[name];
    if (value === undefined) throw new Error(`Missing path parameter: ${name}`);
    return encodeURIComponent(String(value));
  });
}
