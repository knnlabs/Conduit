import { NextRequest, NextResponse } from 'next/server';

export async function GET(req: NextRequest) {
  return NextResponse.json(
    { 
      error: 'Usage analytics export is not available',
      message: 'The backend endpoints required for usage analytics export do not exist' 
    },
    { status: 501 }
  );
}