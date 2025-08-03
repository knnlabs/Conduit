import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { TransactionHistoryParams } from '@knn_labs/conduit-admin-client';

type Params = Promise<{ id: string }>;

// GET /api/virtualkeys/groups/:id/transactions - Get transaction history
export async function GET(request: NextRequest, { params }: { params: Params }) {
  try {
    const { id } = await params;
    const groupId = parseInt(id, 10);

    if (isNaN(groupId)) {
      return NextResponse.json(
        { error: 'Invalid group ID' },
        { status: 400 }
      );
    }

    const searchParams = request.nextUrl.searchParams;
    const page = searchParams.get('page');
    const pageSize = searchParams.get('pageSize');

    const queryParams: TransactionHistoryParams = {};
    if (page) {
      queryParams.page = parseInt(page, 10);
    }
    if (pageSize) {
      queryParams.pageSize = parseInt(pageSize, 10);
    }

    const adminClient = getServerAdminClient();
    const transactions = await adminClient.virtualKeyGroups.getTransactionHistory(groupId, queryParams);

    return NextResponse.json(transactions);
  } catch (error) {
    console.error('[VirtualKeyGroups] Error fetching transaction history:', error);
    return handleSDKError(error);
  }
}