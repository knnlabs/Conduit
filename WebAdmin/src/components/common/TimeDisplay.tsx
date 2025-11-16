'use client';

import { useEffect, useState } from 'react';

interface TimeDisplayProps {
  date: Date | string;
  format?: 'time' | 'datetime';
}

export function TimeDisplay({ date, format = 'time' }: TimeDisplayProps) {
  const [mounted, setMounted] = useState(false);
  
  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;
  
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  
  return (
    <>
      {format === 'datetime' 
        ? dateObj.toLocaleString() 
        : dateObj.toLocaleTimeString()}
    </>
  );
}