import { NextRequest, NextResponse } from 'next/server';

export async function GET(_request: NextRequest) {
  // Caching configuration is part of Operations features
  // Will be implemented after Configuration pages are complete
  return NextResponse.json(
    { 
      message: 'Caching configuration is part of Operations features and will be available soon',
      settings: [],
      policies: [],
      statistics: null
    },
    { status: 501 } // Not Implemented
  );
}

export async function POST(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Caching configuration is part of Operations features and will be available soon'
    },
    { status: 501 }
  );
}

export async function PUT(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Caching configuration is part of Operations features and will be available soon'
    },
    { status: 501 }
  );
}