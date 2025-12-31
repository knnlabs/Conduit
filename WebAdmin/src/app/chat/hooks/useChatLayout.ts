import { useState, useEffect } from 'react';

const STORAGE_KEYS = {
  SETTINGS_EXPANDED: 'chat-settings-expanded',
  PARAMETERS_EXPANDED: 'chat-parameters-expanded',
  COMPACT_MODE: 'chat-compact-mode',
  MESSAGES_HEIGHT: 'chat-messages-height',
} as const;

interface ChatLayoutState {
  settingsExpanded: boolean;
  parametersExpanded: boolean;
  compactMode: boolean;
  messagesHeight: number;
}

interface ChatLayoutActions {
  toggleSettings: () => void;
  toggleParameters: () => void;
  toggleCompactMode: () => void;
  setMessagesHeight: (height: number) => void;
  collapseAll: () => void;
  expandAll: () => void;
}

export type ChatLayoutReturn = ChatLayoutState & ChatLayoutActions;

const DEFAULT_MESSAGES_HEIGHT = 500; // Default height in pixels

export function useChatLayout(): ChatLayoutReturn {
  // Initialize state from localStorage or defaults
  const [settingsExpanded, setSettingsExpanded] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    const stored = localStorage.getItem(STORAGE_KEYS.SETTINGS_EXPANDED);
    return stored !== null ? JSON.parse(stored) as boolean : false;
  });

  const [parametersExpanded, setParametersExpanded] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    const stored = localStorage.getItem(STORAGE_KEYS.PARAMETERS_EXPANDED);
    return stored !== null ? JSON.parse(stored) as boolean : false;
  });

  const [compactMode, setCompactMode] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false;
    const stored = localStorage.getItem(STORAGE_KEYS.COMPACT_MODE);
    return stored !== null ? JSON.parse(stored) as boolean : false;
  });

  const [messagesHeight, setMessagesHeight] = useState<number>(() => {
    if (typeof window === 'undefined') return DEFAULT_MESSAGES_HEIGHT;
    const stored = localStorage.getItem(STORAGE_KEYS.MESSAGES_HEIGHT);
    return stored !== null ? parseInt(stored, 10) : DEFAULT_MESSAGES_HEIGHT;
  });

  // Persist to localStorage
  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.SETTINGS_EXPANDED, JSON.stringify(settingsExpanded));
  }, [settingsExpanded]);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.PARAMETERS_EXPANDED, JSON.stringify(parametersExpanded));
  }, [parametersExpanded]);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.COMPACT_MODE, JSON.stringify(compactMode));
  }, [compactMode]);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.MESSAGES_HEIGHT, messagesHeight.toString());
  }, [messagesHeight]);

  const toggleSettings = () => setSettingsExpanded(prev => !prev);
  const toggleParameters = () => setParametersExpanded(prev => !prev);
  const toggleCompactMode = () => {
    setCompactMode(prev => {
      const newValue = !prev;
      // When entering compact mode, collapse everything
      if (newValue) {
        setSettingsExpanded(false);
        setParametersExpanded(false);
      }
      return newValue;
    });
  };

  const collapseAll = () => {
    setSettingsExpanded(false);
    setParametersExpanded(false);
  };

  const expandAll = () => {
    setSettingsExpanded(true);
    setParametersExpanded(true);
  };

  const handleSetMessagesHeight = (height: number) => {
    // Clamp between reasonable values
    const MIN_HEIGHT = 200;
    const MAX_HEIGHT = 1200;
    const clampedHeight = Math.max(MIN_HEIGHT, Math.min(MAX_HEIGHT, height));
    setMessagesHeight(clampedHeight);
  };

  return {
    settingsExpanded,
    parametersExpanded,
    compactMode,
    messagesHeight,
    toggleSettings,
    toggleParameters,
    toggleCompactMode,
    setMessagesHeight: handleSetMessagesHeight,
    collapseAll,
    expandAll,
  };
}
