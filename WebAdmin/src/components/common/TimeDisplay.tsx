'use client';

import { useEffect, useState } from 'react';
import { formatRelativeTime } from '@/lib/utils/formatters';

interface TimeDisplayProps {
  date: Date | string;
  format?: 'time' | 'datetime' | 'relative';
}

export function TimeDisplay({ date, format = 'time' }: TimeDisplayProps) {
  const [mounted, setMounted] = useState(false);
  
  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;
  
  const dateObj = typeof date === 'string' ? new Date(date) : date;

  if (format === 'relative') {
    return <>{formatRelativeTime(dateObj)}</>;
  }
  
  return (
    <>
      {format === 'datetime' 
        ? dateObj.toLocaleString() 
        : dateObj.toLocaleTimeString()}
    </>
  );
}
