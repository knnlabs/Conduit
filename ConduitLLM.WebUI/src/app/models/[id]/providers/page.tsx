import { notFound } from 'next/navigation';

interface PageProps {
  params: Promise<{
    id: string;
  }>;
}

export default async function ModelProvidersPage({ params }: PageProps) {
  const { id } = await params;
  
  // This page is not implemented yet
  notFound();
}