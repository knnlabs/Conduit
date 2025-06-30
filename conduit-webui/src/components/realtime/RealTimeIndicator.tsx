'use client';

import { Badge, Transition } from '@mantine/core';
import { IconActivity } from '@tabler/icons-react';
import { useEffect, useState } from 'react';

interface RealTimeIndicatorProps {
  isActive: boolean;
  label?: string;
  color?: string;
}

export function RealTimeIndicator({ 
  isActive, 
  label = 'Live', 
  color = 'green' 
}: RealTimeIndicatorProps) {
  const [showPulse, setShowPulse] = useState(false);

  useEffect(() => {
    if (isActive) {
      setShowPulse(true);
      const timer = setTimeout(() => setShowPulse(false), 2000);
      return () => clearTimeout(timer);
    }
  }, [isActive]);

  return (
    <Transition
      mounted={isActive}
      transition="fade"
      duration={400}
      timingFunction="ease"
    >
      {(styles) => (
        <Badge
          style={styles}
          size="sm"
          color={color}
          variant="dot"
          leftSection={
            <IconActivity 
              size={12} 
              className={showPulse ? 'animate-pulse' : ''}
            />
          }
        >
          {label}
        </Badge>
      )}
    </Transition>
  );
}