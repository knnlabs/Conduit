import { NextResponse } from 'next/server';
import { config } from '@/config';

// SignalR negotiate endpoint placeholder
// In production, SignalR connections go directly to the backend APIs
// This is just a placeholder to prevent 404 errors during development

export async function POST() {
  return NextResponse.json({
    message: 'SignalR connections should be made directly to the backend APIs',
    coreApiHub: `${config.api.external.coreUrl}/hubs/navigation-state`,
    adminApiHub: `${config.api.external.adminUrl}/hubs/admin`,
  });
}

export async function GET() {
  return POST();
}