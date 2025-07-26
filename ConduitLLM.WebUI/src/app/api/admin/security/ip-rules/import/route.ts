import { NextRequest, NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';
import { getServerAdminClient } from '@/lib/server/adminClient';
import type { IpFilterImport } from '@knn_labs/conduit-admin-client';

// POST /api/admin/security/ip-rules/import - Import IP rules
export async function POST(req: NextRequest) {
  try {
    const formData = await req.formData();
    const file = formData.get('file') as File;
    const format = formData.get('format') as string;
    
    if (!file) {
      return NextResponse.json({ error: 'No file provided' }, { status: 400 });
    }
    
    if (format !== 'json' && format !== 'csv') {
      return NextResponse.json({ error: 'Invalid format. Must be json or csv' }, { status: 400 });
    }
    
    const client = getServerAdminClient();
    const text = await file.text();
    
    let rules: IpFilterImport[] = [];
    
    if (format === 'json') {
      try {
        rules = JSON.parse(text) as IpFilterImport[];
      } catch {
        return NextResponse.json({ error: 'Invalid JSON format' }, { status: 400 });
      }
    } else {
      // Parse CSV
      const lines = text.split('\n').filter(line => line.trim());
      const headers = lines[0]?.toLowerCase().split(',').map(h => h.trim());
      
      if (!headers || !headers.includes('ipaddress') || !headers.includes('action')) {
        return NextResponse.json({ 
          error: 'CSV must have headers including "ipAddress" and "action"' 
        }, { status: 400 });
      }
      
      for (let i = 1; i < lines.length; i++) {
        const values = lines[i].split(',').map(v => v.trim());
        const ipIndex = headers.indexOf('ipaddress');
        const ruleIndex = headers.indexOf('action');
        const descIndex = headers.indexOf('description');
        
        if (values[ipIndex] && values[ruleIndex]) {
          rules.push({
            ipAddress: values[ipIndex],
            action: values[ruleIndex] as 'allow' | 'block',
            description: descIndex >= 0 ? values[descIndex] : undefined,
          });
        }
      }
    }
    
    if (rules.length === 0) {
      return NextResponse.json({ error: 'No valid rules found in file' }, { status: 400 });
    }
    
    const result = await client.ipFilter.import(rules);
    
    return NextResponse.json({
      imported: result.imported,
      failed: result.failed,
      error: result.failed > 0 ? `${result.failed} rules failed to import` : undefined,
    });
  } catch (error) {
    return handleSDKError(error);
  }
}