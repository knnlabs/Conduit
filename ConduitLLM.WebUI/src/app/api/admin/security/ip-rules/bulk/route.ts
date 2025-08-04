import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';

// POST /api/admin/security/ip-rules/bulk - Bulk operations
export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as {
      operation: 'enable' | 'disable' | 'delete';
      ruleIds: string[];
    };
    
    const client = getServerAdminClient();
    
    // Bulk operations no longer exist in the API
    // Process each rule individually
    const results = await Promise.all(
      body.ruleIds.map(async (ruleId) => {
        try {
          const numericId = parseInt(ruleId, 10);
          if (body.operation === 'delete') {
            await client.ipFilters.deleteById(numericId);
            return { id: ruleId, success: true };
          } else if (body.operation === 'enable') {
            await client.ipFilters.enableFilter(numericId);
            return { id: ruleId, success: true };
          } else if (body.operation === 'disable') {
            await client.ipFilters.disableFilter(numericId);
            return { id: ruleId, success: true };
          }
        } catch (error) {
          console.error(`Failed to ${body.operation} rule ${ruleId}:`, error);
          return { id: ruleId, success: false, error: String(error) };
        }
      })
    );
    
    // Return result summary
    const successCount = results.filter(r => r?.success).length;
    return NextResponse.json({
      success: successCount === results.length,
      processed: results.length,
      succeeded: successCount,
      failed: results.length - successCount,
      results
    });
  } catch (error) {
    return handleSDKError(error);
  }
}