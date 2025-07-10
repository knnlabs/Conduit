import { NextRequest, NextResponse } from "next/server";

export async function POST(_request: NextRequest) {
  return NextResponse.json(
    { 
      message: "Cache clearing is part of Operations features and will be available soon"
    },
    { status: 501 }
  );
}
