import { NextResponse } from 'next/server';

export async function GET() {
  return NextResponse.json(
    { 
      error: 'Virtual keys analytics export is not available',
      message: 'The backend endpoints required for virtual keys analytics export do not exist' 
    },
    { status: 501 }
  );
}