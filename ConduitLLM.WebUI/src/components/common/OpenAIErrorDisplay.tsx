'use client';

import {
  Alert,
  Button,
  Stack,
  Text,
  Group,
  List,
  Collapse,
  Box,
  Paper,
  Center,
  Container,
  Badge,
  ThemeIcon,
} from '@mantine/core';
import {
  IconAlertTriangle,
  IconAlertCircle,
  IconRefresh,
  IconClock,
  IconCreditCard,
  IconLock,
  IconSearch,
  IconServer,
  IconFileText,
  IconExclamationCircle as IconExclamation,
  IconChevronDown,
  IconChevronUp,
  IconInfoCircle,
} from '@tabler/icons-react';
import { useState } from 'react';
import type { AppError } from '@/utils/errorHandling';

export interface OpenAIErrorDisplayProps {
  error: AppError;
  variant?: 'inline' | 'card' | 'fullscreen';
  size?: 'sm' | 'md' | 'lg';
  showDetails?: boolean;
  showSuggestions?: boolean;
  onRetry?: () => void;
  actions?: Array<{
    label: string;
    onClick: () => void;
    color?: string;
    variant?: 'filled' | 'light' | 'outline' | 'subtle';
  }>;
  className?: string;
  testId?: string;
}

const iconMap: Record<string, React.ReactNode> = {
  LockClosedIcon: <IconLock size={20} />,
  CreditCardIcon: <IconCreditCard size={20} />,
  MagnifyingGlassIcon: <IconSearch size={20} />,
  ClockIcon: <IconClock size={20} />,
  DocumentTextIcon: <IconFileText size={20} />,
  ExclamationTriangleIcon: <IconAlertTriangle size={20} />,
  ServerIcon: <IconServer size={20} />,
  ExclamationCircleIcon: <IconAlertCircle size={20} />,
};

export function OpenAIErrorDisplay({
  error,
  variant = 'inline',
  size = 'md',
  showDetails = true,
  showSuggestions = true,
  onRetry,
  actions = [],
  className,
  testId,
}: OpenAIErrorDisplayProps) {
  const [detailsOpen, setDetailsOpen] = useState(false);
  
  // Get the appropriate icon
  const icon = iconMap[error.iconName] ?? <IconExclamation size={20} />;
  
  // Determine alert color based on severity
  const getAlertColor = () => {
    switch (error.severity) {
      case 'error':
        return 'red';
      case 'warning':
        return 'orange';
      case 'info':
        return 'blue';
      default:
        return 'gray';
    }
  };
  
  // Build action buttons
  const actionButtons = [...actions];
  
  // Add retry button if error is recoverable
  if (error.isRecoverable && onRetry && !actions.find(a => a.label === 'Try Again')) {
    actionButtons.unshift({
      label: error.retryAfter ? `Retry in ${error.retryAfter}s` : 'Try Again',
      onClick: onRetry,
      color: 'blue',
      variant: 'light',
    });
  }
  
  const alertColor = getAlertColor();
  const hasAdditionalInfo = showDetails && (
    error.originalError ?? 
    error.code !== 'unknown_error' ?? 
    error.retryAfter
  );
  
  // Error content
  const errorContent = (
    <Stack gap={size === 'sm' ? 'xs' : 'sm'}>
      <Alert
        icon={icon}
        title={error.title}
        color={alertColor}
        variant="light"
        className={className}
        data-testid={testId}
      >
        <Stack gap={size === 'sm' ? 'xs' : 'sm'}>
          {/* Main error message */}
          <Text size={size === 'sm' ? 'sm' : 'md'}>
            {error.message}
          </Text>
          
          {/* Status and error code badges */}
          <Group gap="xs">
            <Badge size="sm" variant="light" color={alertColor}>
              Status: {error.status}
            </Badge>
            {error.code && error.code !== 'unknown_error' && (
              <Badge size="sm" variant="light" color="gray">
                Code: {error.code}
              </Badge>
            )}
            {error.retryAfter && (
              <Badge size="sm" variant="light" color="blue">
                Retry after: {error.retryAfter}s
              </Badge>
            )}
          </Group>
          
          {/* Suggestions */}
          {showSuggestions && error.suggestions.length > 0 && (
            <Box>
              <Group gap={4} mb={4}>
                <ThemeIcon size="xs" variant="light" color="blue">
                  <IconInfoCircle size={12} />
                </ThemeIcon>
                <Text size="sm" fw={500}>What you can do:</Text>
              </Group>
              <List size="sm" spacing="xs" withPadding>
                {error.suggestions.map((suggestion, index) => (
                  <List.Item key={index}>
                    <Text size="sm" c="dimmed">{suggestion}</Text>
                  </List.Item>
                ))}
              </List>
            </Box>
          )}
          
          {/* Details toggle */}
          {hasAdditionalInfo && (
            <>
              <Button
                variant="subtle"
                size="xs"
                onClick={() => setDetailsOpen(!detailsOpen)}
                leftSection={detailsOpen ? <IconChevronUp size={14} /> : <IconChevronDown size={14} />}
                style={{ alignSelf: 'flex-start' }}
              >
                {detailsOpen ? 'Hide' : 'Show'} Technical Details
              </Button>
              
              <Collapse in={detailsOpen}>
                <Paper withBorder p="sm" bg="gray.0" radius="sm">
                  <Stack gap="xs">
                    {error.originalError && (
                      <>
                        <Group gap="xs">
                          <Text size="xs" c="dimmed" fw={500}>Type:</Text>
                          <Text size="xs" c="dimmed">{error.originalError.type}</Text>
                        </Group>
                        {error.originalError.param && (
                          <Group gap="xs">
                            <Text size="xs" c="dimmed" fw={500}>Parameter:</Text>
                            <Text size="xs" c="dimmed">{error.originalError.param}</Text>
                          </Group>
                        )}
                      </>
                    )}
                    <Group gap="xs">
                      <Text size="xs" c="dimmed" fw={500}>Error Code:</Text>
                      <Text size="xs" c="dimmed">{error.code}</Text>
                    </Group>
                    <Group gap="xs">
                      <Text size="xs" c="dimmed" fw={500}>HTTP Status:</Text>
                      <Text size="xs" c="dimmed">{error.status}</Text>
                    </Group>
                  </Stack>
                </Paper>
              </Collapse>
            </>
          )}
          
          {/* Action buttons */}
          {actionButtons.length > 0 && (
            <Group gap="sm" mt="xs">
              {actionButtons.map((action, index) => (
                <Button
                  key={index}
                  size={size === 'sm' ? 'xs' : 'sm'}
                  variant={action.variant ?? 'light'}
                  color={action.color ?? 'blue'}
                  leftSection={action.label.includes('Try') ? <IconRefresh size={16} /> : undefined}
                  onClick={action.onClick}
                  disabled={error.retryAfter && action.label === 'Try Again' ? true : undefined}
                >
                  {action.label}
                </Button>
              ))}
            </Group>
          )}
        </Stack>
      </Alert>
    </Stack>
  );
  
  // Render based on variant
  switch (variant) {
    case 'card':
      return (
        <Paper withBorder radius="md" p="md" className={className}>
          {errorContent}
        </Paper>
      );
      
    case 'fullscreen':
      return (
        <Container size="sm" py="xl">
          <Center style={{ minHeight: 'calc(100vh - 200px)' }}>
            <Box style={{ width: '100%', maxWidth: 600 }}>
              <Paper withBorder radius="lg" p="xl" shadow="md">
                {errorContent}
              </Paper>
            </Box>
          </Center>
        </Container>
      );
      
    case 'inline':
    default:
      return errorContent;
  }
}

// Convenience component for model not found errors
export function ModelNotFoundErrorDisplay({
  modelName,
  onSelectDifferentModel,
  ...props
}: Omit<OpenAIErrorDisplayProps, 'error'> & {
  modelName: string;
  onSelectDifferentModel?: () => void;
}) {
  const error: AppError = {
    status: 404,
    code: 'model_not_found',
    title: 'Model Not Found',
    message: `The model "${modelName}" is not available. Please select a different model.`,
    isRecoverable: false,
    suggestions: [
      'Check available models in the model selector',
      'Contact support if you need access to this model',
      'Try using an alternative model with similar capabilities',
    ],
    severity: 'warning',
    iconName: 'MagnifyingGlassIcon',
  };
  
  const actions = props.actions ?? [];
  if (onSelectDifferentModel) {
    actions.push({
      label: 'Select Different Model',
      onClick: onSelectDifferentModel,
      color: 'blue',
      variant: 'filled',
    });
  }
  
  return <OpenAIErrorDisplay {...props} error={error} actions={actions} />;
}

// Convenience component for rate limit errors
export function RateLimitErrorDisplay({
  retryAfter,
  ...props
}: Omit<OpenAIErrorDisplayProps, 'error'> & {
  retryAfter?: number;
}) {
  const error: AppError = {
    status: 429,
    code: 'rate_limit_exceeded',
    title: 'Rate Limit Exceeded',
    message: retryAfter 
      ? `Rate limit exceeded. Please wait ${retryAfter} seconds before trying again.`
      : 'You have exceeded the rate limit. Please slow down your requests.',
    isRecoverable: true,
    suggestions: [
      'Wait before retrying your request',
      'Consider upgrading your plan for higher limits',
      'Implement request batching to reduce API calls',
    ],
    retryAfter,
    severity: 'warning',
    iconName: 'ClockIcon',
  };
  
  return <OpenAIErrorDisplay {...props} error={error} />;
}