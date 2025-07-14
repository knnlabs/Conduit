import { requireAdmin } from '@/lib/auth/server-auth-check';

export default async function ProtectedLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  // Check admin status for all protected routes
  await requireAdmin();
  
  return <>{children}</>;
}