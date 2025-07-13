import { NextRequest, NextResponse } from 'next/server';
import { requireAuth } from '@/lib/auth/simple-auth';
import { getServerAdminClient } from '@/lib/server/adminClient';
import { handleSDKError } from '@/lib/errors/sdk-errors';

// POST /api/admin/security/ip-rules/import - Import IP rules
export async function POST(req: NextRequest) {
  const auth = requireAuth(req);
  if (!auth.isValid) {
    return auth.response!;
  }

  try {
    const formData = await req.formData();
    const file = formData.get('file') as File;
    const format = formData.get('format') as string || 'json';
    
    if (!file) {
      return NextResponse.json(
        { error: 'No file provided' },
        { status: 400 }
      );
    }

    const adminClient = getServerAdminClient();
    
    if (!adminClient.ipFilters) {
      return NextResponse.json(
        { error: 'IP filtering not available' },
        { status: 501 }
      );
    }

    const content = await file.text();
    let rules: any[] = [];

    if (format === 'json') {
      try {
        rules = JSON.parse(content);
      } catch (err) {
        return NextResponse.json(
          { error: 'Invalid JSON format' },
          { status: 400 }
        );
      }
    } else if (format === 'csv') {
      // Parse CSV
      const lines = content.split('\n').filter(line => line.trim());
      const headers = lines[0].toLowerCase().split(',').map(h => h.trim());
      
      // Find column indices
      const ipIndex = headers.findIndex(h => h.includes('ip') || h.includes('address'));
      const actionIndex = headers.findIndex(h => h.includes('action') || h.includes('type'));
      const descIndex = headers.findIndex(h => h.includes('desc'));
      
      if (ipIndex === -1 || actionIndex === -1) {
        return NextResponse.json(
          { error: 'CSV must have IP Address and Action columns' },
          { status: 400 }
        );
      }

      // Parse data rows
      for (let i = 1; i < lines.length; i++) {
        const values = lines[i].split(',').map(v => v.trim().replace(/^"|"$/g, ''));
        if (values[ipIndex]) {
          rules.push({
            ipAddress: values[ipIndex],
            action: values[actionIndex].toLowerCase(),
            description: descIndex !== -1 ? values[descIndex] : '',
          });
        }
      }
    } else {
      return NextResponse.json(
        { error: 'Invalid format. Use json or csv' },
        { status: 400 }
      );
    }

    // Transform rules to SDK import format
    const importRules = rules.map(rule => ({
      ipAddress: rule.ipAddress,
      rule: (rule.action === 'allow' ? 'allow' : 'deny') as 'allow' | 'deny',
      description: rule.description,
    }));

    // Import rules
    const result = await adminClient.ipFilters.import(importRules);
    
    return NextResponse.json({
      success: true,
      imported: result.imported || 0,
      failed: result.failed || 0,
      errors: result.errors || [],
    });
  } catch (error) {
    return handleSDKError(error);
  }
}