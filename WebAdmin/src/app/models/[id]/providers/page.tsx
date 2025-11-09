import { notFound } from 'next/navigation';

interface PageProps {
  params: Promise<{
    id: string;
  }>;
}

export default async function ModelProvidersPage({ params }: PageProps) {
  // Await params to satisfy Next.js typing
  await params;
  
  // This page is not implemented yet
  notFound();
}