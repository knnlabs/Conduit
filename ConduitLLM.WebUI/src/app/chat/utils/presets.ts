import { ConversationStarter } from '../types';
import { 
  IconBrain, 
  IconCode, 
  IconPencil, 
  IconChartBar, 
  IconMessageCircle,
  IconBulb,
  IconBrush,
  IconHeart,
  IconTool
} from '@tabler/icons-react';

// Re-export presets from Core SDK
export { 
  CHAT_PRESETS,
  getPresetById,
  findMatchingPreset,
  applyPreset,
  getDefaultPreset,
  PresetCategory,
  PRESET_CATEGORIES,
  getPresetsByCategory,
} from '@knn_labs/conduit-core-client';

export const CONVERSATION_STARTERS: ConversationStarter[] = [
  {
    id: 'explain-concept',
    title: 'Explain a Complex Concept',
    prompt: 'Can you explain quantum computing in simple terms that a beginner could understand?',
    category: 'Learning',
    icon: 'brain',
  },
  {
    id: 'debug-code',
    title: 'Debug Code',
    prompt: 'I have a bug in my code. Here\'s what\'s happening: [describe the issue]. Can you help me debug it?',
    category: 'Programming',
    icon: 'code',
  },
  {
    id: 'creative-story',
    title: 'Write a Story',
    prompt: 'Write a short story about a world where AI and humans collaborate to solve climate change.',
    category: 'Creative',
    icon: 'pencil',
  },
  {
    id: 'data-analysis',
    title: 'Analyze Data',
    prompt: 'I have sales data showing [describe data]. What insights can you help me extract from this?',
    category: 'Analysis',
    icon: 'chart-bar',
  },
  {
    id: 'brainstorm-ideas',
    title: 'Brainstorm Ideas',
    prompt: 'Help me brainstorm innovative ideas for a sustainable packaging solution for e-commerce.',
    category: 'Innovation',
    icon: 'bulb',
  },
  {
    id: 'learn-topic',
    title: 'Learn New Topic',
    prompt: 'I want to learn about machine learning. Can you create a learning path for a complete beginner?',
    category: 'Learning',
    icon: 'book',
  },
  {
    id: 'sql-query',
    title: 'Write SQL Query',
    prompt: 'Help me write an SQL query to find the top 10 customers by total purchase amount in the last year.',
    category: 'Programming',
    icon: 'database',
  },
  {
    id: 'design-feedback',
    title: 'Design Feedback',
    prompt: 'I\'m designing a mobile app for fitness tracking. What UI/UX best practices should I consider?',
    category: 'Design',
    icon: 'brush',
  },
  {
    id: 'personal-growth',
    title: 'Personal Development',
    prompt: 'What are effective strategies for improving focus and productivity while working from home?',
    category: 'Personal',
    icon: 'heart',
  },
  {
    id: 'technical-architecture',
    title: 'System Architecture',
    prompt: 'I need to design a scalable microservices architecture. What are the key considerations?',
    category: 'Technical',
    icon: 'tool',
  },
];

export const getCategoryIcon = (category: string) => {
  const iconMap: Record<string, React.ComponentType<{ size?: number; className?: string; stroke?: number }>> = {
    Learning: IconBrain,
    Programming: IconCode,
    Creative: IconPencil,
    Analysis: IconChartBar,
    Innovation: IconBulb,
    Design: IconBrush,
    Personal: IconHeart,
    Technical: IconTool,
  };
  return iconMap[category] ?? IconMessageCircle;
};

export const getPresetIcon = (iconName: string) => {
  const iconMap: Record<string, React.ComponentType<{ size?: number; className?: string; stroke?: number }>> = {
    ['message-circle']: IconMessageCircle,
    'pencil': IconPencil,
    'code': IconCode,
    ['chart-bar']: IconChartBar,
    'brain': IconBrain,
  };
  return iconMap[iconName] ?? IconMessageCircle;
};