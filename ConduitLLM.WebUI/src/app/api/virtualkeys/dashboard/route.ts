import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { 
  VirtualKeyListResponseDto
} from '@knn_labs/conduit-admin-client';

export async function GET(req: NextRequest) {

  try {
    const { searchParams } = new URL(req.url);
    const period = searchParams.get('period') ?? '30d';
    const adminClient = getServerAdminClient();
    
    // Calculate date range
    const now = new Date();
    const startDate = new Date();
    
    switch (period) {
      case '7d':
        startDate.setDate(now.getDate() - 7);
        break;
      case '30d':
        startDate.setDate(now.getDate() - 30);
        break;
      case '90d':
        startDate.setDate(now.getDate() - 90);
        break;
      default:
        startDate.setDate(now.getDate() - 30);
    }
    
    try {
      // Try to get analytics data, but don't fail if unavailable
      
      // Analytics endpoints don't exist - just get virtual keys
      const allKeysResponse: VirtualKeyListResponseDto = await adminClient.virtualKeys.list(1, 100);
      
      // Transform virtual keys data without analytics
      const virtualKeysData = allKeysResponse.items.map((key) => {
        return {
          id: key.id,
          name: key.keyName,
          status: key.isEnabled ? 'active' : 'inactive',
          requests: 0,
          cost: 0,
          budget: key.maxBudget ?? 0,
          budgetUsed: 0,
        };
      });
      
      // No analytics data available
      const totalRequests = 0;
      const totalCost = 0;
      const activeKeys = virtualKeysData.filter((k) => k.status === 'active').length;
      const averageBudgetUsed = 0;
      
      // Growth rates not available
      const requestsGrowth = null;
      const costGrowth = null;
      const activeKeysGrowth = null;
      
      // No time series data available
      const timeSeriesData: Array<Record<string, unknown>> = [];
      
      // No model usage data available
      const modelUsage: Array<Record<string, unknown>> = [];
      
      return NextResponse.json({
        virtualKeys: virtualKeysData,
        summary: {
          totalRequests,
          totalCost,
          activeKeys,
          averageBudgetUsed,
          requestsGrowth,
          costGrowth,
          activeKeysGrowth,
        },
        timeSeriesData,
        modelUsage,
      });
    } catch (sdkError) {
      console.error('SDK Error in dashboard:', sdkError);
      
      // If analytics methods fail, provide fallback with just virtual keys data
      const allKeysResponse: VirtualKeyListResponseDto = await adminClient.virtualKeys.list(1, 100);
      
      const virtualKeysData = allKeysResponse.items.map((key) => ({
        id: key.id,
        name: key.keyName,
        status: key.isEnabled ? 'active' : 'inactive',
        requests: 0,
        cost: 0,
        budget: key.maxBudget ?? 0,
        budgetUsed: 0,
      }));
      
      // Return minimal data structure
      return NextResponse.json({
        virtualKeys: virtualKeysData,
        summary: {
          totalRequests: 0,
          totalCost: 0,
          activeKeys: virtualKeysData.filter((k) => k.status === 'active').length,
          averageBudgetUsed: 0,
          requestsGrowth: null,
          costGrowth: null,
          activeKeysGrowth: null,
        },
        timeSeriesData: [],
        modelUsage: [],
      });
    }
  } catch (error) {
    return handleSDKError(error);
  }
}