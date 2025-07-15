import { NextRequest, NextResponse } from 'next/server';

export async function POST(request: NextRequest) {
  const text = await request.text();
  console.log('Test login received:', text);
  console.log('Admin password from env:', process.env.CONDUIT_ADMIN_LOGIN_PASSWORD);
  
  return NextResponse.json({ 
    received: text,
    envSet: !!process.env.CONDUIT_ADMIN_LOGIN_PASSWORD 
  });
}