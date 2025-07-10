import { NextRequest, NextResponse } from 'next/server';

export async function GET(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Security features are part of Operations and will be available soon',
      data: []
    },
    { status: 501 }
  );
}

export async function POST(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Security features are part of Operations and will be available soon'
    },
    { status: 501 }
  );
}

export async function PUT(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Security features are part of Operations and will be available soon'
    },
    { status: 501 }
  );
}

export async function DELETE(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Security features are part of Operations and will be available soon'
    },
    { status: 501 }
  );
}