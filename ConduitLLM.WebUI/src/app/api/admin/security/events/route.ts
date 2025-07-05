import { NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse } from '@/lib/errors/sdk-errors';
import { parseQueryParams } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, context) => {
    try {
      const params = parseQueryParams(request);
      const hours = parseInt(params.get('hours') || '24');
      
      // Calculate date range from hours
      const endDate = new Date().toISOString();
      const startDate = new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
      
      // Get security events using SDK
      const result = await context.adminClient!.security.getEvents({
        startDate,
        endDate,
        pageSize: 100,
      });
      
      // Transform to match expected format
      const eventsByType: Record<string, number> = {};
      const eventsBySeverity: Record<string, number> = {};
      
      result.items.forEach(event => {
        eventsByType[event.type] = (eventsByType[event.type] || 0) + 1;
        eventsBySeverity[event.severity] = (eventsBySeverity[event.severity] || 0) + 1;
      });
      
      return NextResponse.json({
        events: result.items,
        timeRange: { start: startDate, end: endDate },
        totalEvents: result.totalCount,
        eventsByType,
        eventsBySeverity,
      });
    } catch (_error: unknown) {
      // Return empty result for failures
      return NextResponse.json({
        events: [],
        timeRange: null,
        totalEvents: 0,
        eventsByType: {},
        eventsBySeverity: {}
      });
    }
  },
  { requireAdmin: true }
);

// Report a security event
export const POST = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      
      // Validate required fields
      if (!body.type || !body.severity || !body.source) {
        return NextResponse.json(
          {
            error: 'Missing required fields',
            message: 'type, severity, and source are required',
          },
          { status: 400 }
        );
      }
      
      // Create the security event using the SDK
      const event = await context.adminClient!.security.reportEvent({
        type: body.type,
        severity: body.severity,
        source: body.source,
        virtualKeyId: body.virtualKeyId,
        ipAddress: body.ipAddress,
        details: body.details || {},
        statusCode: body.statusCode,
      });
      
      return NextResponse.json(event);
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Export security events
export const PUT = withSDKAuth(
  async (request, context) => {
    try {
      const body = await request.json();
      const { format = 'json', filters = {} } = body;
      const hours = filters.hours || 24;
      
      // Calculate date range from hours
      const endDate = new Date().toISOString();
      const startDate = new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
      
      // Get security events using SDK
      const result = await context.adminClient!.security.getEvents({
        startDate,
        endDate,
        severity: filters.severity,
        type: filters.type,
        pageSize: 1000, // Get more events for export
      });
      
      const events = result.items || [];
      
      // Calculate aggregates
      const eventsByType: Record<string, number> = {};
      const eventsBySeverity: Record<string, number> = {};
      
      events.forEach(event => {
        eventsByType[event.type] = (eventsByType[event.type] || 0) + 1;
        eventsBySeverity[event.severity] = (eventsBySeverity[event.severity] || 0) + 1;
      });
      
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `security-events-${timestamp}.${format}`;
      
      let content: string;
      let contentType: string;
      
      if (format === 'csv') {
        // Convert events to CSV
        const headers = 'Timestamp,Type,Severity,Source,VirtualKeyId,Details';
        const rows = events.map((e) => 
          `${e.timestamp},${e.type},${e.severity},${e.source},${e.virtualKeyId || ''},"${JSON.stringify(e.details)}"`
        ).join('\n');
        content = `${headers}\n${rows}`;
        contentType = 'text/csv';
      } else {
        // JSON format
        content = JSON.stringify({
          exportDate: new Date().toISOString(),
          timeRange: { start: startDate, end: endDate },
          totalEvents: result.totalCount,
          eventsByType,
          eventsBySeverity,
          events: events,
          metadata: {
            totalRecords: events.length,
            exportVersion: '1.0'
          }
        }, null, 2);
        contentType = 'application/json';
      }

      return new Response(content, {
        headers: {
          'Content-Type': contentType,
          'Content-Disposition': `attachment; filename="${filename}"`,
          'Cache-Control': 'no-cache',
        },
      });
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);