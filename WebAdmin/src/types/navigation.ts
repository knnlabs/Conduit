
export interface NavigationItem {
  id: string;
  label: string;
  icon: React.ComponentType<{ size?: number }>;
  href: string;
  description?: string;
  badge?: string | number;
  color?: string;
  disabled?: boolean;
  children?: NavigationItem[];
}

export interface NavigationSection {
  id: string;
  label: string;
  items: NavigationItem[];
  collapsed?: boolean;
}


export interface ConnectionStatus {
  coreApi: 'connected' | 'disconnected' | 'connecting' | 'error';
  adminApi: 'connected' | 'disconnected' | 'connecting' | 'error';
  signalR: 'connected' | 'disconnected' | 'connecting' | 'reconnecting' | 'error';
  lastCheck: Date | null;
}