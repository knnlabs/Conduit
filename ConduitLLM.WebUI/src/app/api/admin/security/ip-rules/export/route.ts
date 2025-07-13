import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// GET /api/admin/security/ip-rules/export - Export IP rules
export async function GET(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const { searchParams } = new URL(req.url);
    const format = searchParams.get('format') || 'json';
    
    if (!['json', 'csv'].includes(format)) {
      return NextResponse.json(
        { error: 'Invalid format. Use json or csv' },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      // Return empty data if service not available
      if (format === 'json') {
        return NextResponse.json([]);
      } else {
        return new NextResponse('IP Address,Action,Description,Created At\n', {
          headers: {
            'Content-Type': 'text/csv',
            'Content-Disposition': 'attachment; filename="ip-rules.csv"',
          },
        });
      }
    }

    // For now, we'll export all rules
    const filters = await adminClient.ipFilters.list();
    
    if (format === 'json') {
      const rules = filters.map(filter => ({
        ipAddress: filter.ipAddressOrCidr,
        action: filter.filterType === 'whitelist' ? 'allow' : 'block',
        description: filter.description || '',
        createdAt: filter.createdAt,
        isEnabled: filter.isEnabled,
      }));
      
      return NextResponse.json(rules, {
        headers: {
          'Content-Disposition': 'attachment; filename="ip-rules.json"',
        },
      });
    } else {
      // CSV format
      let csv = 'IP Address,Action,Description,Enabled,Created At\n';
      
      filters.forEach(filter => {
        const row = [
          filter.ipAddressOrCidr,
          filter.filterType === 'whitelist' ? 'allow' : 'block',
          `"${(filter.description || '').replace(/"/g, '""')}"`,
          filter.isEnabled ? 'yes' : 'no',
          filter.createdAt,
        ].join(',');
        csv += row + '\n';
      });
      
      return new NextResponse(csv, {
        headers: {
          'Content-Type': 'text/csv',
          'Content-Disposition': 'attachment; filename="ip-rules.csv"',
        },
      });
    }
  } catch (error) {
    return handleSDKError(error);
  }
}