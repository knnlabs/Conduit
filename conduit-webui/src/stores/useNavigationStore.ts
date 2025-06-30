import { create } from 'zustand';
import { NavigationState } from '@/types/navigation';
import { navigationSections } from '@/lib/navigation/items';

export const useNavigationStore = create<NavigationState>((set, get) => ({
  sections: navigationSections,
  activeItem: null,
  isLoading: false,
  lastUpdated: null,

  setActiveItem: (itemId: string) => {
    set({ activeItem: itemId });
  },

  toggleSection: (sectionId: string) => {
    set((state) => ({
      sections: state.sections.map((section) =>
        section.id === sectionId
          ? { ...section, collapsed: !section.collapsed }
          : section
      ),
    }));
  },

  updateNavigationState: (newState: Partial<NavigationState>) => {
    set((state) => ({
      ...state,
      ...newState,
      lastUpdated: new Date(),
    }));
  },

  refreshNavigationState: async () => {
    set({ isLoading: true });

    try {
      // TODO: Implement real-time navigation state updates
      // This will be connected to SignalR for dynamic menu updates
      // based on provider health, model availability, etc.
      
      // For now, just update the timestamp
      set({ 
        isLoading: false, 
        lastUpdated: new Date() 
      });
    } catch (error) {
      console.error('Failed to refresh navigation state:', error);
      set({ isLoading: false });
    }
  },
}));