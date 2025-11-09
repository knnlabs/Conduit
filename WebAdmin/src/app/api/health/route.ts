import { NextResponse } from 'next/server';

export async function GET() {
  try {
    return NextResponse.json({
      status: 'healthy',
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      memory: process.memoryUsage().rss
    });
  } catch {
    return NextResponse.json(
      { 
        status: 'unhealthy',
        timestamp: new Date().toISOString()
      },
      { status: 500 }
    );
  }
}