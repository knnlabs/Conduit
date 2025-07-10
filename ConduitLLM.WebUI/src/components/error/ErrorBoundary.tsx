'use client';

import React, { Component, ErrorInfo, ReactNode } from 'react';
import { Alert, Button, Code, Container, Stack, Text } from '@mantine/core';
import { IconAlertTriangle, IconRefresh } from '@tabler/icons-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
    };
  }

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      error,
    };
  }

  override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    // Log error details to console
    console.error('ErrorBoundary caught an error:', error, errorInfo);
  }

  handleReset = () => {
    this.setState({
      hasError: false,
      error: null,
    });
  };

  override render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return <>{this.props.fallback}</>;
      }

      const { error } = this.state;

      return (
        <Container size="sm" py="xl">
          <Stack gap="lg" align="center" ta="center">
            <Alert
              icon={<IconAlertTriangle size={24} />}
              title="Something went wrong"
              color="red"
              variant="light"
              styles={{ root: { width: '100%' } }}
            >
              <Stack gap="sm">
                <Text size="sm">
                  An unexpected error occurred. Please try refreshing the page or contact support if the problem persists.
                </Text>
                {process.env.NODE_ENV === 'development' && error && (
                  <Code block color="red" style={{ textAlign: 'left', fontSize: '0.8rem' }}>
                    {error.message}
                    {error.stack && '\n\n' + error.stack}
                  </Code>
                )}
              </Stack>
            </Alert>

            <Button
              leftSection={<IconRefresh size={16} />}
              onClick={this.handleReset}
              variant="light"
            >
              Try Again
            </Button>
          </Stack>
        </Container>
      );
    }

    return this.props.children;
  }
}