import { NextRequest, NextResponse } from 'next/server';

export async function GET(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Request logs is part of Operations features and will be available soon',
      logs: [],
      pagination: {
        page: 1,
        pageSize: 20,
        totalItems: 0,
        totalPages: 0
      }
    },
    { status: 501 }
  );
}

export async function POST(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: 'Request logs export is part of Operations features and will be available soon'
    },
    { status: 501 }
  );
}