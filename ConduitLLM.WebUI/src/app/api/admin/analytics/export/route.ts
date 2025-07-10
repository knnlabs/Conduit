import { NextRequest, NextResponse } from 'next/server';

export async function POST(_request: NextRequest) {
  // Analytics export is part of Operations features
  // Will be implemented after Configuration pages are complete
  return NextResponse.json(
    { 
      message: 'Analytics export is part of Operations features and will be available soon'
    },
    { status: 501 } // Not Implemented
  );
}