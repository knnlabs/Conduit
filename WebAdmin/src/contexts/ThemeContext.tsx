'use client';

import { createContext, useContext, useEffect, useState, useCallback, ReactNode } from 'react';
// Removed unused MantineColorScheme import

type ThemeMode = 'light' | 'dark' | 'auto';

interface ThemeContextType {
  mode: ThemeMode;
  colorScheme: 'light' | 'dark';
  setMode: (mode: ThemeMode) => void;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

export function useTheme() {
  const context = useContext(ThemeContext);
  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
}

interface ThemeProviderProps {
  children: ReactNode;
}

export function ThemeProvider({ children }: ThemeProviderProps) {
  const [mode, setMode] = useState<ThemeMode>('auto');
  const [colorScheme, setColorScheme] = useState<'light' | 'dark'>('light');

  // Function to detect system theme
  const getSystemTheme = (): 'light' | 'dark' => {
    if (typeof window !== 'undefined' && window.matchMedia) {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return 'light';
  };

  // Update color scheme based on mode
  const updateColorScheme = useCallback((newMode: ThemeMode) => {
    let newColorScheme: 'light' | 'dark';
    
    if (newMode === 'auto') {
      newColorScheme = getSystemTheme();
    } else {
      newColorScheme = newMode;
    }
    
    setColorScheme(newColorScheme);
  }, []);

  // Handle mode changes
  const handleSetMode = (newMode: ThemeMode) => {
    setMode(newMode);
    updateColorScheme(newMode);
    
    // Save to localStorage
    if (typeof window !== 'undefined') {
      localStorage.setItem('theme-mode', newMode);
    }
  };

  // Initialize theme on mount
  useEffect(() => {
    // Load saved theme mode from localStorage
    const savedMode = typeof window !== 'undefined' 
      ? (localStorage.getItem('theme-mode') as ThemeMode) || 'auto'
      : 'auto';
    
    setMode(savedMode);
    updateColorScheme(savedMode);

    // Listen for system theme changes when in auto mode
    if (typeof window !== 'undefined' && window.matchMedia) {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      
      const handleSystemThemeChange = (e: MediaQueryListEvent) => {
        if (mode === 'auto') {
          setColorScheme(e.matches ? 'dark' : 'light');
        }
      };

      mediaQuery.addEventListener('change', handleSystemThemeChange);
      
      return () => {
        mediaQuery.removeEventListener('change', handleSystemThemeChange);
      };
    }
  }, [mode, updateColorScheme]);

  // Update color scheme when mode changes
  useEffect(() => {
    updateColorScheme(mode);
  }, [mode, updateColorScheme]);

  const value: ThemeContextType = {
    mode,
    colorScheme,
    setMode: handleSetMode,
  };

  return (
    <ThemeContext.Provider value={value}>
      {children}
    </ThemeContext.Provider>
  );
}