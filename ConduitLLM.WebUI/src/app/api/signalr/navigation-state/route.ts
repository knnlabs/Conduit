import { NextResponse } from 'next/server';

// SignalR negotiate endpoint placeholder
// In production, SignalR connections go directly to the backend APIs
// This is just a placeholder to prevent 404 errors during development

export async function POST() {
  // Since external URLs are server-side only, we return the SignalR config endpoint
  return NextResponse.json({
    message: 'Use /api/signalr/config to get SignalR connection URLs',
    configEndpoint: '/api/signalr/config',
  });
}

export async function GET() {
  return POST();
}