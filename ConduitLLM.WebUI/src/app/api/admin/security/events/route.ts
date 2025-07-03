import { NextRequest, NextResponse } from 'next/server';
import { withSDKAuth } from '@/lib/auth/sdk-auth';
import { mapSDKErrorToResponse, withSDKErrorHandling } from '@/lib/errors/sdk-errors';
import { parseQueryParams } from '@/lib/utils/route-helpers';

export const GET = withSDKAuth(
  async (request, { auth }) => {
    try {
      const params = parseQueryParams(request);
      const hours = parseInt(params.get('hours') || '24');
      
      // Call the Admin API's security events endpoint directly
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL}/api/security/events?hours=${hours}`,
        {
          headers: {
            'Authorization': `Bearer ${process.env.CONDUIT_MASTER_KEY}`,
            'Content-Type': 'application/json',
          },
        }
      );
      
      if (!response.ok) {
        throw new Error(`Security events API returned ${response.status}`);
      }
      
      const data = await response.json();
      
      // Return the data directly
      return NextResponse.json(data);
    } catch (error: any) {
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

// Report a security event (not yet implemented in backend)
export const POST = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      
      // Backend doesn't have a security event reporting endpoint yet
      // Return a placeholder response indicating the feature is not available
      return NextResponse.json(
        {
          message: 'Security event reporting is not yet implemented',
          received: body,
        },
        {
          status: 501, // Not Implemented
        }
      );
    } catch (error) {
      return mapSDKErrorToResponse(error);
    }
  },
  { requireAdmin: true }
);

// Export security events
export const PUT = withSDKAuth(
  async (request, { auth }) => {
    try {
      const body = await request.json();
      const { format = 'json', filters = {} } = body;
      const hours = filters.hours || 24;
      
      // Fetch security events from the Admin API
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_CONDUIT_ADMIN_API_URL}/api/security/events?hours=${hours}`,
        {
          headers: {
            'Authorization': `Bearer ${process.env.CONDUIT_MASTER_KEY}`,
            'Content-Type': 'application/json',
          },
        }
      );
      
      if (!response.ok) {
        throw new Error(`Security events API returned ${response.status}`);
      }
      
      const data = await response.json();
      const events = data.events || [];
      
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `security-events-${timestamp}.${format}`;
      
      let content: string;
      let contentType: string;
      
      if (format === 'csv') {
        // Convert events to CSV
        const headers = 'Timestamp,Type,Severity,Source,VirtualKeyId,Details';
        const rows = events.map((e: any) => 
          `${e.timestamp},${e.type},${e.severity},${e.source},${e.virtualKeyId || ''},"${e.details}"`
        ).join('\n');
        content = `${headers}\n${rows}`;
        contentType = 'text/csv';
      } else {
        // JSON format
        content = JSON.stringify({
          exportDate: new Date().toISOString(),
          timeRange: data.timeRange,
          totalEvents: data.totalEvents,
          eventsByType: data.eventsByType,
          eventsBySeverity: data.eventsBySeverity,
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