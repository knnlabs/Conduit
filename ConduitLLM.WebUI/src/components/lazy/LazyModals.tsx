import { lazy } from 'react';
import { withSuspense } from '@/lib/utils/lazyLoad';
import { Skeleton } from '@mantine/core';

// Modal skeleton loader
const ModalSkeleton = () => (
  <Skeleton height={400} radius="md" />
);

// Virtual Key Modals
const CreateVirtualKeyModalLazy = lazy(() => import('../virtualkeys/CreateVirtualKeyModal').then(mod => ({ default: mod.CreateVirtualKeyModal })));
export const LazyCreateVirtualKeyModal = withSuspense(CreateVirtualKeyModalLazy, <ModalSkeleton />);

const EditVirtualKeyModalLazy = lazy(() => import('../virtualkeys/EditVirtualKeyModal').then(mod => ({ default: mod.EditVirtualKeyModal })));
export const LazyEditVirtualKeyModal = withSuspense(EditVirtualKeyModalLazy, <ModalSkeleton />);

const ViewVirtualKeyModalLazy = lazy(() => import('../virtualkeys/ViewVirtualKeyModal').then(mod => ({ default: mod.ViewVirtualKeyModal })));
export const LazyViewVirtualKeyModal = withSuspense(ViewVirtualKeyModalLazy, <ModalSkeleton />);

// Provider Modals
const CreateProviderModalLazy = lazy(() => import('../providers/CreateProviderModal').then(mod => ({ default: mod.CreateProviderModal })));
export const LazyCreateProviderModal = withSuspense(CreateProviderModalLazy, <ModalSkeleton />);

const EditProviderModalLazy = lazy(() => import('../providers/EditProviderModal').then(mod => ({ default: mod.EditProviderModal })));
export const LazyEditProviderModal = withSuspense(EditProviderModalLazy, <ModalSkeleton />);

// Model Mapping Modals
const CreateModelMappingModalLazy = lazy(() => import('../modelmappings/CreateModelMappingModal').then(mod => ({ default: mod.CreateModelMappingModal })));
export const LazyCreateModelMappingModal = withSuspense(CreateModelMappingModalLazy, <ModalSkeleton />);

const EditModelMappingModalLazy = lazy(() => import('../modelmappings/EditModelMappingModal').then(mod => ({ default: mod.EditModelMappingModal })));
export const LazyEditModelMappingModal = withSuspense(EditModelMappingModalLazy, <ModalSkeleton />);