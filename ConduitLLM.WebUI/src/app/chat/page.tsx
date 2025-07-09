import { lazyLoadPage } from '@/lib/utils/lazyLoad';

const ChatPage = lazyLoadPage(
  () => import('./ChatInterface'),
  { 
    loadingMessage: 'Loading chat interface...',
    moduleName: 'Chat Interface'
  }
);

export default ChatPage;