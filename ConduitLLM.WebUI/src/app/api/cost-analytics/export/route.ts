import { NextResponse } from 'next/server';

export async function POST() {
  return NextResponse.json(
    { 
      error: 'Cost analytics export is not available',
      message: 'The backend endpoints required for cost analytics export do not exist' 
    },
    { status: 501 }
  );
}