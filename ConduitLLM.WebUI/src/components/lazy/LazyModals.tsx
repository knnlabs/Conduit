import { lazy, Suspense } from 'react';
import { Skeleton } from '@mantine/core';

// Modal skeleton loader
const ModalSkeleton = () => (
  <Skeleton height={400} radius="md" />
);

// Virtual Key Modals
const CreateVirtualKeyModalLazy = lazy(() => import('../virtualkeys/CreateVirtualKeyModal').then(mod => ({ default: mod.CreateVirtualKeyModal })));
export const LazyCreateVirtualKeyModal = (props: React.ComponentProps<typeof CreateVirtualKeyModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <CreateVirtualKeyModalLazy {...props} />
  </Suspense>
);

const EditVirtualKeyModalLazy = lazy(() => import('../virtualkeys/EditVirtualKeyModal').then(mod => ({ default: mod.EditVirtualKeyModal })));
export const LazyEditVirtualKeyModal = (props: React.ComponentProps<typeof EditVirtualKeyModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <EditVirtualKeyModalLazy {...props} />
  </Suspense>
);

const ViewVirtualKeyModalLazy = lazy(() => import('../virtualkeys/ViewVirtualKeyModal').then(mod => ({ default: mod.ViewVirtualKeyModal })));
export const LazyViewVirtualKeyModal = (props: React.ComponentProps<typeof ViewVirtualKeyModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <ViewVirtualKeyModalLazy {...props} />
  </Suspense>
);

// Provider Modals
const CreateProviderModalLazy = lazy(() => import('../providers/CreateProviderModal').then(mod => ({ default: mod.CreateProviderModal })));
export const LazyCreateProviderModal = (props: React.ComponentProps<typeof CreateProviderModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <CreateProviderModalLazy {...props} />
  </Suspense>
);

const EditProviderModalLazy = lazy(() => import('../providers/EditProviderModal').then(mod => ({ default: mod.EditProviderModal })));
export const LazyEditProviderModal = (props: React.ComponentProps<typeof EditProviderModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <EditProviderModalLazy {...props} />
  </Suspense>
);

// Model Mapping Modals
const CreateModelMappingModalLazy = lazy(() => import('../modelmappings/CreateModelMappingModal').then(mod => ({ default: mod.CreateModelMappingModal })));
export const LazyCreateModelMappingModal = (props: React.ComponentProps<typeof CreateModelMappingModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <CreateModelMappingModalLazy {...props} />
  </Suspense>
);

const EditModelMappingModalLazy = lazy(() => import('../modelmappings/EditModelMappingModal').then(mod => ({ default: mod.EditModelMappingModal })));
export const LazyEditModelMappingModal = (props: React.ComponentProps<typeof EditModelMappingModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <EditModelMappingModalLazy {...props} />
  </Suspense>
);