'use client';

import {
  Badge,
  Group,
  Tooltip,
  Indicator,
  ActionIcon,
  Text,
  ThemeIcon,
  Box,
} from '@mantine/core';
import {
  IconCheck,
  IconX,
  IconAlertTriangle,
  IconAlertCircle,
  IconClock,
  IconActivity,
  IconWifiOff,
  IconTool,
  IconHelpCircle,
  IconLoader,
} from '@tabler/icons-react';
import { useStatusIndicator, type SystemStatusType } from '@/hooks/useSystemStatus';

// Icon mapping for status types
const STATUS_ICONS = {
  'check': IconCheck,
  'x': IconX,
  'check-circle': IconCheck,
  'x-circle': IconX,
  'alert-triangle': IconAlertTriangle,
  'alert-circle': IconAlertCircle,
  'clock': IconClock,
  'activity': IconActivity,
  'wifi-off': IconWifiOff,
  'tool': IconTool,
  'help-circle': IconHelpCircle,
  'loader': IconLoader,
} as const;

export interface StatusIndicatorProps {
  status: SystemStatusType | boolean;
  variant?: 'badge' | 'icon' | 'dot' | 'text' | 'detailed';
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl';
  context?: string;
  label?: string;
  description?: string;
  showTooltip?: boolean;
  animate?: boolean;
  className?: string;
  testId?: string;
}

export function StatusIndicator({
  status,
  variant = 'badge',
  size = 'sm',
  context,
  label,
  description,
  showTooltip = true,
  animate = false,
  className,
  testId,
}: StatusIndicatorProps) {
  const statusConfig = useStatusIndicator(status, context);
  
  const displayLabel = label || statusConfig.label;
  const displayDescription = description || statusConfig.description;
  
  // Get appropriate icon component
  const IconComponent = statusConfig.icon ? STATUS_ICONS[statusConfig.icon as keyof typeof STATUS_ICONS] : IconHelpCircle;
  
  // Animate icons for loading/connecting states
  const shouldAnimate = animate || ['connecting', 'processing', 'pending'].includes(statusConfig.type);
  
  const iconStyle = shouldAnimate ? {
    animation: 'spin 2s linear infinite',
  } : undefined;

  const renderIcon = (iconSize = 16) => (
    <IconComponent 
      size={iconSize} 
      style={iconStyle}
      data-testid={testId ? `${testId}-icon` : undefined}
    />
  );

  const renderContent = () => {
    switch (variant) {
      case 'badge':
        return (
          <Badge
            color={statusConfig.color}
            variant="light"
            size={size}
            leftSection={renderIcon(12)}
            className={className}
            data-testid={testId}
          >
            {displayLabel}
          </Badge>
        );

      case 'icon':
        return (
          <ThemeIcon
            color={statusConfig.color}
            variant="light"
            size={size}
            className={className}
            data-testid={testId}
          >
            {renderIcon()}
          </ThemeIcon>
        );

      case 'dot':
        return (
          <Indicator
            color={statusConfig.color}
            size={size === 'xs' ? 6 : size === 'sm' ? 8 : size === 'md' ? 10 : 12}
            className={className}
            data-testid={testId}
          />
        );

      case 'text':
        return (
          <Group gap="xs" className={className} data-testid={testId}>
            {renderIcon(14)}
            <Text size={size} c={statusConfig.color}>
              {displayLabel}
            </Text>
          </Group>
        );

      case 'detailed':
        return (
          <Group gap="xs" className={className} data-testid={testId}>
            <Indicator color={statusConfig.color} size={8} />
            <Box>
              <Text size={size} fw={500}>
                {displayLabel}
              </Text>
              {displayDescription && (
                <Text size="xs" c="dimmed">
                  {displayDescription}
                </Text>
              )}
            </Box>
          </Group>
        );

      default:
        return null;
    }
  };

  const content = renderContent();
  
  if (!content) return null;

  // Wrap with tooltip if enabled and we have description
  if (showTooltip && displayDescription && variant !== 'detailed') {
    return (
      <Tooltip label={displayDescription} position="top">
        {content}
      </Tooltip>
    );
  }

  return content;
}

// Specialized status indicator components for common use cases
export function HealthStatusIndicator({
  status,
  ...props
}: Omit<StatusIndicatorProps, 'context'> & { status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown' }) {
  return <StatusIndicator {...props} status={status} context="health" />;
}

export function ConnectionStatusIndicator({
  status,
  ...props
}: Omit<StatusIndicatorProps, 'context'> & { status: 'connected' | 'connecting' | 'disconnected' | 'error' }) {
  return <StatusIndicator {...props} status={status} context="connection" />;
}

export function EnabledStatusIndicator({
  enabled,
  labels,
  ...props
}: Omit<StatusIndicatorProps, 'status' | 'label'> & { 
  enabled: boolean; 
  labels?: { enabled?: string; disabled?: string; };
}) {
  return (
    <StatusIndicator 
      {...props} 
      status={enabled} 
      label={enabled ? (labels?.enabled || 'Enabled') : (labels?.disabled || 'Disabled')}
    />
  );
}

export function TaskStatusIndicator({
  status,
  ...props
}: Omit<StatusIndicatorProps, 'context'> & { status: 'pending' | 'processing' | 'completed' | 'failed' }) {
  return <StatusIndicator {...props} status={status} context="task" animate />;
}

// Composite status indicator for complex status displays
export interface CompositeStatusIndicatorProps {
  primary: SystemStatusType | boolean;
  secondary?: SystemStatusType | boolean;
  label: string;
  description?: string;
  variant?: 'horizontal' | 'vertical';
  size?: 'xs' | 'sm' | 'md' | 'lg';
  className?: string;
  testId?: string;
}

export function CompositeStatusIndicator({
  primary,
  secondary,
  label,
  description,
  variant = 'horizontal',
  size = 'sm',
  className,
  testId,
}: CompositeStatusIndicatorProps) {
  const primaryConfig = useStatusIndicator(primary);
  const secondaryConfig = secondary ? useStatusIndicator(secondary) : null;

  if (variant === 'vertical') {
    return (
      <Box className={className} data-testid={testId}>
        <Group gap="xs" mb={4}>
          <StatusIndicator status={primary} variant="dot" size={size} showTooltip={false} />
          <Text size={size} fw={500}>
            {label}
          </Text>
        </Group>
        {description && (
          <Text size="xs" c="dimmed" ml={18}>
            {description}
          </Text>
        )}
        {secondaryConfig && (
          <Group gap="xs" mt={4} ml={18}>
            <StatusIndicator status={secondary!} variant="dot" size="xs" showTooltip={false} />
            <Text size="xs" c="dimmed">
              {secondaryConfig.label}
            </Text>
          </Group>
        )}
      </Box>
    );
  }

  return (
    <Group gap="sm" className={className} data-testid={testId}>
      <StatusIndicator status={primary} variant="dot" size={size} showTooltip={false} />
      <Box style={{ flex: 1 }}>
        <Text size={size} fw={500}>
          {label}
        </Text>
        {description && (
          <Text size="xs" c="dimmed">
            {description}
          </Text>
        )}
      </Box>
      {secondaryConfig && (
        <StatusIndicator status={secondary!} variant="icon" size="xs" />
      )}
    </Group>
  );
}

// Helper component for status with refresh capability
export interface RefreshableStatusIndicatorProps extends StatusIndicatorProps {
  onRefresh?: () => void;
  isRefreshing?: boolean;
  lastUpdate?: Date;
}

export function RefreshableStatusIndicator({
  onRefresh,
  isRefreshing = false,
  lastUpdate,
  ...props
}: RefreshableStatusIndicatorProps) {
  return (
    <Group gap="xs">
      <StatusIndicator {...props} />
      {onRefresh && (
        <ActionIcon
          size="sm"
          variant="subtle"
          onClick={onRefresh}
          loading={isRefreshing}
          title="Refresh status"
        >
          <IconLoader size={14} />
        </ActionIcon>
      )}
      {lastUpdate && (
        <Text size="xs" c="dimmed">
          {lastUpdate.toLocaleTimeString()}
        </Text>
      )}
    </Group>
  );
}