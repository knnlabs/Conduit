import { NextResponse } from 'next/server';

// SignalR negotiate endpoint placeholder
// In production, SignalR connections go directly to the backend APIs
// This is just a placeholder to prevent 404 errors during development

export async function POST() {
  return NextResponse.json({
    message: 'SignalR connections should be made directly to the backend APIs',
    coreApiHub: `${process.env.CONDUIT_API_EXTERNAL_URL || 'http://localhost:5000'}/hubs/navigation-state`,
    adminApiHub: `${process.env.CONDUIT_ADMIN_API_EXTERNAL_URL || 'http://localhost:5002'}/hubs/admin`,
  });
}

export async function GET() {
  return POST();
}