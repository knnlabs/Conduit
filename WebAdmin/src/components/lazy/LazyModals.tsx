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

// Virtual Key Group Modals
const CreateVirtualKeyGroupModalLazy = lazy(() => import('../virtualkeys/CreateVirtualKeyGroupModal').then(mod => ({ default: mod.CreateVirtualKeyGroupModal })));
export const LazyCreateVirtualKeyGroupModal = (props: React.ComponentProps<typeof CreateVirtualKeyGroupModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <CreateVirtualKeyGroupModalLazy {...props} />
  </Suspense>
);

const EditVirtualKeyGroupModalLazy = lazy(() => import('../virtualkeys/EditVirtualKeyGroupModal').then(mod => ({ default: mod.EditVirtualKeyGroupModal })));
export const LazyEditVirtualKeyGroupModal = (props: React.ComponentProps<typeof EditVirtualKeyGroupModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <EditVirtualKeyGroupModalLazy {...props} />
  </Suspense>
);

const ViewVirtualKeyGroupModalLazy = lazy(() => import('../virtualkeys/ViewVirtualKeyGroupModal').then(mod => ({ default: mod.ViewVirtualKeyGroupModal })));
export const LazyViewVirtualKeyGroupModal = (props: React.ComponentProps<typeof ViewVirtualKeyGroupModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <ViewVirtualKeyGroupModalLazy {...props} />
  </Suspense>
);

const AddCreditsModalLazy = lazy(() => import('../virtualkeys/AddCreditsModal').then(mod => ({ default: mod.AddCreditsModal })));
export const LazyAddCreditsModal = (props: React.ComponentProps<typeof AddCreditsModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <AddCreditsModalLazy {...props} />
  </Suspense>
);

const TransactionHistoryModalLazy = lazy(() => import('../virtualkeys/TransactionHistoryModal').then(mod => ({ default: mod.TransactionHistoryModal })));
export const LazyTransactionHistoryModal = (props: React.ComponentProps<typeof TransactionHistoryModalLazy>) => (
  <Suspense fallback={<ModalSkeleton />}>
    <TransactionHistoryModalLazy {...props} />
  </Suspense>
);

