import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface GeneratedImage {
  id: string;
  prompt: string;
  url: string;
  model: string;
  size: string;
  quality: string;
  style?: string;
  createdAt: Date;
  cost: number;
}

interface ImageGenerationState {
  images: GeneratedImage[];
  selectedVirtualKey: string;
  totalCost: number;
  addImage: (image: GeneratedImage) => void;
  removeImage: (id: string) => void;
  clearImages: () => void;
  setSelectedVirtualKey: (key: string) => void;
}

export const useImageGenerationStore = create<ImageGenerationState>()(
  persist(
    (set) => ({
      images: [],
      selectedVirtualKey: '',
      totalCost: 0,

      addImage: (image) =>
        set((state) => ({
          images: [image, ...state.images],
          totalCost: state.totalCost + image.cost,
        })),

      removeImage: (id) =>
        set((state) => {
          const imageToRemove = state.images.find((img) => img.id === id);
          if (!imageToRemove) return state;

          return {
            images: state.images.filter((img) => img.id !== id),
            totalCost: state.totalCost - imageToRemove.cost,
          };
        }),

      clearImages: () =>
        set({
          images: [],
          totalCost: 0,
        }),

      setSelectedVirtualKey: (key) =>
        set({
          selectedVirtualKey: key,
        }),
    }),
    {
      name: 'image-generation-store',
      partialize: (state) => ({
        images: state.images,
        selectedVirtualKey: state.selectedVirtualKey,
        totalCost: state.totalCost,
      }),
    }
  )
);