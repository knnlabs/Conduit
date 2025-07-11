import { NextResponse } from 'next/server';
import { handleSDKError } from '@/lib/errors/sdk-errors';

export async function POST(_request: Request) {
  try {
    const response = NextResponse.json({
      success: true,
      message: 'Logged out successfully',
    });

    // Clear the session cookie
    response.cookies.set('conduit_session', '', {
      httpOnly: true,
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'strict',
      maxAge: 0, // Expire immediately
      expires: new Date(0), // Set to past date
    });

    return response;
  } catch (error) {
    return handleSDKError(error);
  }
}
