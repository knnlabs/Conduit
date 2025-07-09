import { lazyLoadComponent } from '@/lib/utils/lazyLoad';
import { Skeleton } from '@mantine/core';

// Modal skeleton loader
const ModalSkeleton = () => (
  <Skeleton height={400} radius="md" />
);

// Virtual Key Modals
export const LazyCreateVirtualKeyModal = lazyLoadComponent(
  () => import('../virtualkeys/CreateVirtualKeyModal').then(mod => ({ default: mod.CreateVirtualKeyModal })),
  <ModalSkeleton />
);

export const LazyEditVirtualKeyModal = lazyLoadComponent(
  () => import('../virtualkeys/EditVirtualKeyModal').then(mod => ({ default: mod.EditVirtualKeyModal })),
  <ModalSkeleton />
);

export const LazyViewVirtualKeyModal = lazyLoadComponent(
  () => import('../virtualkeys/ViewVirtualKeyModal').then(mod => ({ default: mod.ViewVirtualKeyModal })),
  <ModalSkeleton />
);

// Provider Modals
export const LazyCreateProviderModal = lazyLoadComponent(
  () => import('../providers/CreateProviderModal').then(mod => ({ default: mod.CreateProviderModal })),
  <ModalSkeleton />
);

export const LazyEditProviderModal = lazyLoadComponent(
  () => import('../providers/EditProviderModal').then(mod => ({ default: mod.EditProviderModal })),
  <ModalSkeleton />
);

// Model Mapping Modals
export const LazyCreateModelMappingModal = lazyLoadComponent(
  () => import('../modelmappings/CreateModelMappingModal').then(mod => ({ default: mod.CreateModelMappingModal })),
  <ModalSkeleton />
);

export const LazyEditModelMappingModal = lazyLoadComponent(
  () => import('../modelmappings/EditModelMappingModal').then(mod => ({ default: mod.EditModelMappingModal })),
  <ModalSkeleton />
);

// Security Modals
export const LazyCreateSecurityEventModal = lazyLoadComponent(
  () => import('../security/CreateSecurityEventModal').then(mod => ({ default: mod.CreateSecurityEventModal })),
  <ModalSkeleton />
);