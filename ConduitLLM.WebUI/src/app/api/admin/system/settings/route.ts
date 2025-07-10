import { NextRequest, NextResponse } from 'next/server';

export async function GET(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'System settings is part of Operations features and will be available soon',
      settings: {}
    },
    { status: 501 }
  );
}

export async function PUT(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'System settings is part of Operations features and will be available soon'
    },
    { status: 501 }
  );
}