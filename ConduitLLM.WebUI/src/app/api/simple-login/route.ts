export const runtime = 'nodejs';

export async function POST(request: Request) {
  // Get the password from form data
  const formData = await request.formData();
  const password = formData.get('password');
  
  // Check against environment variable
  const adminPassword = process.env.CONDUIT_ADMIN_LOGIN_PASSWORD;
  
  if (!adminPassword) {
    return new Response('Server not configured', { status: 500 });
  }
  
  if (password === adminPassword) {
    // Create session cookie
    const sessionData = {
      id: `session_${Date.now()}`,
      isAdmin: true,
      isAuthenticated: true,
      expiresAt: Date.now() + (24 * 60 * 60 * 1000),
      masterKeyHash: 'admin'
    };
    
    // Return response with redirect and cookie
    return new Response(null, {
      status: 302,
      headers: {
        'Location': '/',
        'Set-Cookie': `conduit_session=${encodeURIComponent(JSON.stringify(sessionData))}; Path=/; HttpOnly; SameSite=Strict; Max-Age=86400${process.env.NODE_ENV === 'production' ? '; Secure' : ''}`
      }
    });
  }
  
  // Invalid password
  return new Response(null, {
    status: 302,
    headers: {
      'Location': '/login?error=invalid'
    }
  });
}