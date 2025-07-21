import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
// GET /api/export/status/[exportId] - Get export status
export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ exportId: string }> }
) {

  try {
    const { exportId } = await params;
    
    // For now, return a mock status since export tracking isn't implemented yet
    // In a real implementation, this would query the export status from a database or job queue
    return NextResponse.json({
      id: exportId,
      status: 'completed',
      progress: 100,
      downloadUrl: `/api/export/download/${exportId}`,
      createdAt: new Date().toISOString(),
      completedAt: new Date().toISOString(),
    });
  } catch (error) {
    return handleSDKError(error);
  }
}
