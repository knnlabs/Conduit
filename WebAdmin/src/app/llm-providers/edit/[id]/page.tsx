'use client';

import { ProviderForm } from '@/components/providers/ProviderForm';
import { use } from 'react';

interface EditProviderPageProps {
  params: Promise<{
    id: string;
  }>;
}

export default function EditProviderPage({ params }: EditProviderPageProps) {
  const resolvedParams = use(params);
  const providerId = parseInt(resolvedParams.id, 10);
  
  if (isNaN(providerId)) {
    return <div>Invalid provider ID</div>;
  }
  
  return <ProviderForm mode="edit" providerId={providerId} />;
}