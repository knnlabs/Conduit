# Clerk Authentication Integration

This document describes the optional Clerk authentication support in Conduit WebUI.

## Overview

Conduit WebUI now supports two authentication modes:
1. **Conduit Auth** (default): Uses the existing password-based authentication with `CONDUIT_ADMIN_LOGIN_PASSWORD`
2. **Clerk Auth** (optional): Uses Clerk for authentication with admin-only access control

## Configuration

### Enabling Clerk Authentication

To enable Clerk authentication, set the following environment variables:

```bash
CLERK_PUBLISHABLE_KEY=your_clerk_publishable_key
CLERK_SECRET_KEY=your_clerk_secret_key
```

When these variables are present, the WebUI automatically switches to Clerk authentication mode.

### Admin Access Control

When Clerk is enabled, only users with admin privileges can access the WebUI. Admin status is determined by the user's public metadata in Clerk.

To grant admin access to a user in Clerk:

1. Go to your Clerk dashboard
2. Navigate to Users
3. Select the user you want to make an admin
4. Edit their Public Metadata
5. Add one of the following:

```json
{
  "conduitAdmin": true
}
```

Or:

```json
{
  "role": "admin"
}
```

## Authentication Flow

### With Conduit Auth (Default)
1. User navigates to `/login`
2. Enters the admin password (`CONDUIT_ADMIN_LOGIN_PASSWORD`)
3. Session cookie is created
4. User is redirected to the dashboard

### With Clerk Auth
1. User navigates to any protected route
2. Automatically redirected to `/sign-in` (Clerk's hosted UI)
3. User authenticates with Clerk
4. System checks if user has admin metadata
5. If admin, user is granted access
6. If not admin, user sees "Access Denied" message

## Implementation Details

### Files Added/Modified

1. **Auth Mode Detection** (`src/lib/auth/auth-mode.ts`)
   - Detects which auth mode to use based on environment variables

2. **Clerk Helpers** (`src/lib/auth/clerk-helpers.ts`)
   - Helper functions for checking Clerk authentication and admin status

3. **Unified Auth** (`src/lib/auth/unified-auth.ts`)
   - Provides a unified interface for authentication checks across both modes

4. **Middleware** (`src/middleware.ts`)
   - Updated to support both authentication modes
   - Routes requests to appropriate auth handler

5. **Conditional Auth Provider** (`src/lib/providers/ConditionalAuthProvider.tsx`)
   - Conditionally wraps the app with either ClerkProvider or AuthProvider

6. **Sign-in/Sign-up Pages** (`src/app/sign-in/[[...sign-in]]/page.tsx`, `src/app/sign-up/[[...sign-up]]/page.tsx`)
   - Clerk's authentication pages

7. **Header Component** (`src/components/layout/Header.tsx`)
   - Updated to support logout for both auth modes
   - Shows user info from Clerk when available

## Security Considerations

1. **Admin-Only Access**: The WebUI is an administrative interface. With Clerk enabled, only users with explicit admin metadata can access any part of the site.

2. **No Data Sync**: User data is not synced between Clerk and Conduit. Each auth system maintains its own session.

3. **Health Endpoints**: Health check endpoints (`/api/health/*`) remain publicly accessible in both modes.

4. **Session Management**: 
   - Conduit auth uses cookie-based sessions with 24-hour expiration
   - Clerk manages its own sessions through its SDK

## Migration Notes

- Existing instances can switch between auth modes by adding/removing Clerk environment variables
- No user data migration is required
- Users will need to re-authenticate when switching auth modes
- Active sessions are not preserved when switching modes

## Testing

To test Clerk integration:

1. Set up a Clerk application at https://clerk.com
2. Add the environment variables to your `.env.local`:
   ```
   CLERK_PUBLISHABLE_KEY=pk_test_...
   CLERK_SECRET_KEY=sk_test_...
   ```
3. Create a user in Clerk dashboard
4. Add admin metadata to the user
5. Start the WebUI and verify Clerk login works
6. Verify non-admin users cannot access the WebUI

To test fallback to Conduit auth:

1. Remove or comment out Clerk environment variables
2. Restart the WebUI
3. Verify the traditional login page appears
4. Verify login works with `CONDUIT_ADMIN_LOGIN_PASSWORD`