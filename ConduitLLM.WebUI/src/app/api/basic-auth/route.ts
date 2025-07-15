import { NextRequest, NextResponse } from 'next/server';

export async function POST(request: NextRequest) {
  const formData = await request.formData();
  const password = formData.get('password')?.toString() || '';
  
  if (password === process.env.CONDUIT_ADMIN_LOGIN_PASSWORD) {
    const response = NextResponse.redirect(new URL('/', request.url));
    response.cookies.set('conduit_session', JSON.stringify({
      id: 'session_' + Date.now(),
      isAdmin: true,
      isAuthenticated: true,
      expiresAt: Date.now() + 86400000,
      masterKeyHash: 'admin'
    }), {
      httpOnly: true,
      path: '/',
      maxAge: 86400
    });
    return response;
  }
  
  return NextResponse.redirect(new URL('/login?error=invalid', request.url));
}