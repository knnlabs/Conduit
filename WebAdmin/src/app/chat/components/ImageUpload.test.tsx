import { render } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';

import { ImageUpload } from './ImageUpload';
import type { ImageAttachment } from '../types';

const firstImage: ImageAttachment = {
  url: 'blob:first',
  base64: 'first',
  mimeType: 'image/png',
  size: 100,
  name: 'first.png',
};

const secondImage: ImageAttachment = {
  url: 'blob:second',
  base64: 'second',
  mimeType: 'image/png',
  size: 200,
  name: 'second.png',
};

describe('ImageUpload object URL cleanup', () => {
  const originalRevokeObjectURL = Object.getOwnPropertyDescriptor(URL, 'revokeObjectURL');

  afterEach(() => {
    if (originalRevokeObjectURL) {
      Object.defineProperty(URL, 'revokeObjectURL', originalRevokeObjectURL);
    } else {
      Reflect.deleteProperty(URL, 'revokeObjectURL');
    }
  });

  it('keeps retained URLs alive across prop changes and revokes current URLs on unmount', () => {
    const revokeObjectURL = jest.fn();
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      value: revokeObjectURL,
    });
    const onImagesChange = jest.fn();
    const { rerender, unmount } = render(
      <MantineProvider>
        <ImageUpload images={[firstImage]} onImagesChange={onImagesChange} />
      </MantineProvider>
    );

    rerender(
      <MantineProvider>
        <ImageUpload images={[firstImage, secondImage]} onImagesChange={onImagesChange} />
      </MantineProvider>
    );

    expect(revokeObjectURL).not.toHaveBeenCalled();

    unmount();

    expect(revokeObjectURL).toHaveBeenCalledTimes(2);
    expect(revokeObjectURL).toHaveBeenCalledWith(firstImage.url);
    expect(revokeObjectURL).toHaveBeenCalledWith(secondImage.url);
  });
});
