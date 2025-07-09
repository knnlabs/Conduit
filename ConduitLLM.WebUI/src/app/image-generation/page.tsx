import { lazyLoadPage } from '@/lib/utils/lazyLoad';

const ImageGeneration = lazyLoadPage(
  () => import('./ImageGeneration'),
  { loadingMessage: 'Loading image generation tools...' }
);

export default ImageGeneration;