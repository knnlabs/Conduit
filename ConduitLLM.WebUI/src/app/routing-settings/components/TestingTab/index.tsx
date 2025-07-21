'use client';

import { RoutingTester } from '../RoutingTester';

interface TestingTabProps {
  onLoadingChange: (loading: boolean) => void;
}

export function TestingTab({ onLoadingChange }: TestingTabProps) {
  return <RoutingTester onLoadingChange={onLoadingChange} />;
}