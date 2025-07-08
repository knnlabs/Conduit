'use client';

import React, { Component, ErrorInfo, ReactNode } from 'react';
import { Alert, Button, Code, Paper, Stack, Text, Title } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
  errorCount: number;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
      errorCount: 0,
    };
  }

  static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      error,
      errorInfo: null,
      errorCount: 0,
    };
  }

  override componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    // Log error details to console with full context
    console.error('ErrorBoundary caught an error:', {
      error: {
        message: error.message,
        stack: error.stack,
        name: error.name,
        cause: (error as any).cause,
      },
      errorInfo: {
        componentStack: errorInfo.componentStack,
      },
      timestamp: new Date().toISOString(),
      userAgent: navigator.userAgent,
      url: window.location.href,
    });

    this.setState((prevState) => ({
      error,
      errorInfo,
      errorCount: prevState.errorCount + 1,
    }));

    // You could also send this to an error reporting service
    // sendToErrorReportingService(error, errorInfo);
  }

  handleReset = () => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null,
      errorCount: 0,
    });
  };

  override render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return <>{this.props.fallback}</>;
      }

      const { error, errorInfo, errorCount } = this.state;

      return (
        <Paper p="xl" m="md" withBorder>
          <Stack gap="md">
            <Alert
              icon={<IconAlertTriangle size={24} />}
              title="Application Error"
              color="red"
              variant="filled"
            >
              <Text size="sm">
                An unexpected error occurred. The error details below can help diagnose the issue.
              </Text>
            </Alert>

            <Stack gap="xs">
              <Title order={4}>Error Details</Title>
              <Code block style={{ maxHeight: '200px', overflow: 'auto' }}>
                {error?.toString() || 'Unknown error'}
              </Code>
            </Stack>

            {error?.stack && (
              <Stack gap="xs">
                <Title order={4}>Stack Trace</Title>
                <Code block style={{ maxHeight: '300px', overflow: 'auto' }}>
                  {error.stack}
                </Code>
              </Stack>
            )}

            {errorInfo?.componentStack && (
              <Stack gap="xs">
                <Title order={4}>Component Stack</Title>
                <Code block style={{ maxHeight: '200px', overflow: 'auto' }}>
                  {errorInfo.componentStack}
                </Code>
              </Stack>
            )}

            <Stack gap="xs">
              <Title order={4}>Debug Information</Title>
              <Code block>
                {JSON.stringify(
                  {
                    errorCount,
                    timestamp: new Date().toISOString(),
                    url: window.location.href,
                    userAgent: navigator.userAgent,
                  },
                  null,
                  2
                )}
              </Code>
            </Stack>

            <Button onClick={this.handleReset} variant="filled">
              Try Again
            </Button>
          </Stack>
        </Paper>
      );
    }

    return this.props.children;
  }
}