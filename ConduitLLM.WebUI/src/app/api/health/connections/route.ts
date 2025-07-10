import { NextRequest, NextResponse } from 'next/server';

export async function GET(_request: NextRequest) {
  // Health connections endpoint is part of Operations features
  // Will be implemented after Configuration pages are complete
  return NextResponse.json(
    { 
      message: 'Health connections monitoring is part of Operations features and will be available soon',
      coreApi: 'unknown',
      adminApi: 'unknown',
      lastCheck: new Date()
    },
    { status: 501 } // Not Implemented
  );
}