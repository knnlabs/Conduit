'use client';

import React, { Component, ReactNode } from 'react';
import { Alert, Button, Stack, Text, Title } from '@mantine/core';
import { IconAlertCircle, IconRefresh } from '@tabler/icons-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
  moduleName?: string;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class LazyErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  override componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    console.error('Lazy loading error:', error, errorInfo);
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null });
    // Force re-render by reloading the page
    window.location.reload();
  };

  override render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <Stack align="center" gap="md" p="xl">
          <IconAlertCircle size={48} color="var(--mantine-color-red-6)" />
          <Title order={3}>Failed to load {this.props.moduleName || 'module'}</Title>
          <Text c="dimmed" ta="center" maw={400}>
            {this.state.error?.message || 'An error occurred while loading this page. This might be due to a network issue or a problem with the application.'}
          </Text>
          <Stack gap="xs" align="center">
            <Button
              leftSection={<IconRefresh size={16} />}
              onClick={this.handleRetry}
            >
              Retry Loading
            </Button>
            <Text size="sm" c="dimmed">
              If the problem persists, try refreshing the page
            </Text>
          </Stack>
        </Stack>
      );
    }

    return this.props.children;
  }
}