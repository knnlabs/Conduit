import { NextRequest, NextResponse } from 'next/server';

export async function GET(req: NextRequest) {
  return NextResponse.json(
    { 
      error: 'Virtual keys analytics export is not available',
      message: 'The backend endpoints required for virtual keys analytics export do not exist' 
    },
    { status: 501 }
  );
}