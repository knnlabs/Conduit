'use client';

import { useState } from 'react';
import Image from 'next/image';
import { useImageStore } from '../hooks/useImageStore';
import { GeneratedImage } from '../types';

export default function ImageGallery() {
  const { results, status } = useImageStore();
  const [selectedImage, setSelectedImage] = useState<GeneratedImage | null>(null);

  const handleDownload = async (image: GeneratedImage, index: number) => {
    try {
      let imageData: string;
      let filename: string;

      if (image.url) {
        // Download from URL
        const response = await fetch(image.url);
        const blob = await response.blob();
        imageData = URL.createObjectURL(blob);
        filename = `generated-image-${index + 1}.png`;
      } else if (image.b64Json) {
        // Convert base64 to blob
        const byteCharacters = atob(image.b64Json);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
          byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: 'image/png' });
        imageData = URL.createObjectURL(blob);
        filename = `generated-image-${index + 1}.png`;
      } else {
        console.error('No image data available');
        return;
      }

      // Create download link
      const link = document.createElement('a');
      link.href = imageData;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      // Clean up object URL if it was created from fetch
      if (image.url) {
        URL.revokeObjectURL(imageData);
      }
    } catch (error) {
      console.error('Download failed:', error);
    }
  };

  const getImageSrc = (image: GeneratedImage): string => {
    if (image.url) {
      return image.url;
    } else if (image.b64Json) {
      return `data:image/png;base64,${image.b64Json}`;
    }
    return '';
  };

  const handleImageClick = (image: GeneratedImage) => {
    setSelectedImage(image);
  };

  const closeModal = () => {
    setSelectedImage(null);
  };

  if (status === 'idle' || (status !== 'generating' && results.length === 0)) {
    return (
      <div className="image-gallery">
        <div className="text-center text-gray-500 py-8">
          Generated images will appear here. Enter a prompt and click &quot;Generate Images&quot; to get started.
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="image-gallery">
        {results.map((image, index) => (
          <div key={`image-${image.id ?? index}`} className="image-card">
            <Image
              src={getImageSrc(image)}
              alt={image.revisedPrompt ?? `Generated image ${index + 1}`}
              onClick={() => handleImageClick(image)}
              className="cursor-pointer"
              loading="lazy"
              width={300}
              height={300}
            />
            <div className="image-card-actions">
              <div className="text-sm text-gray-600">
                Image {index + 1}
                {image.revisedPrompt && (
                  <div className="text-xs text-gray-500 mt-1 truncate" title={image.revisedPrompt}>
                    {image.revisedPrompt}
                  </div>
                )}
              </div>
              <button
                onClick={() => void handleDownload(image, index)}
                className="btn btn-secondary text-sm"
                title="Download image"
              >
                📥 Download
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Image Modal */}
      {selectedImage && (
        <div 
          className="fixed inset-0 bg-black bg-opacity-75 flex items-center justify-center z-50 p-4"
          onClick={closeModal}
        >
          <div className="relative max-w-full max-h-full">
            <Image
              src={getImageSrc(selectedImage)}
              alt={selectedImage.revisedPrompt ?? 'Generated image'}
              className="max-w-full max-h-full object-contain"
              onClick={(e) => e.stopPropagation()}
              width={800}
              height={600}
            />
            <button
              onClick={closeModal}
              className="absolute top-4 right-4 text-white bg-black bg-opacity-50 rounded-full w-8 h-8 flex items-center justify-center hover:bg-opacity-75"
            >
              ✕
            </button>
            {selectedImage.revisedPrompt && (
              <div className="absolute bottom-4 left-4 right-4 text-white bg-black bg-opacity-75 p-2 rounded">
                <div className="text-sm">
                  <strong>Revised Prompt:</strong> {selectedImage.revisedPrompt}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </>
  );
}