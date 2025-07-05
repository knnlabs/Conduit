import { NextRequest, NextResponse } from 'next/server';
import { getServerAdminClient, getServerCoreClient } from '@/lib/clients/server';
import { ConnectionStatus } from '@/types/navigation';

export interface ConnectionHealthResponse {
  coreApi: ConnectionStatus['coreApi'];
  adminApi: ConnectionStatus['adminApi'];
  lastCheck: Date;
}

export async function GET(_request: NextRequest) {
  const response: ConnectionHealthResponse = {
    coreApi: 'disconnected',
    adminApi: 'disconnected',
    lastCheck: new Date(),
  };

  try {
    // Check Core API connection using a dummy key for ping
    try {
      const coreClient = await getServerCoreClient('health-check-dummy-key');
      const isConnected = await coreClient.connection.pingWithTimeout(5000);
      response.coreApi = isConnected ? 'connected' : 'error';
    } catch (error) {
      console.warn('Core API connection check failed:', error);
      response.coreApi = 'error';
    }

    // Check Admin API connection
    try {
      const adminClient = await getServerAdminClient();
      const isConnected = await adminClient.connection.pingWithTimeout(5000);
      response.adminApi = isConnected ? 'connected' : 'error';
    } catch (error) {
      console.warn('Admin API connection check failed:', error);
      response.adminApi = 'error';
    }

    return NextResponse.json(response);
  } catch (error) {
    console.error('Connection health check error:', error);
    return NextResponse.json(
      { error: 'Failed to check connections' },
      { status: 500 }
    );
  }
}