import { ChatPreset, ConversationStarter } from '../types';
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

export const CHAT_PRESETS: ChatPreset[] = [
  {
    id: 'balanced',
    name: 'Balanced',
    description: 'Default settings for general conversation',
    icon: 'message-circle',
    parameters: {
      temperature: 0.7,
      topP: 1,
      frequencyPenalty: 0,
      presencePenalty: 0,
    },
  },
  {
    id: 'creative',
    name: 'Creative Writing',
    description: 'Higher creativity for storytelling and brainstorming',
    icon: 'pencil',
    parameters: {
      temperature: 0.9,
      topP: 0.95,
      frequencyPenalty: 0.3,
      presencePenalty: 0.3,
    },
  },
  {
    id: 'code',
    name: 'Code Assistant',
    description: 'Precise responses for programming tasks',
    icon: 'code',
    parameters: {
      temperature: 0.2,
      topP: 0.95,
      frequencyPenalty: 0,
      presencePenalty: 0,
    },
  },
  {
    id: 'analytical',
    name: 'Analytical',
    description: 'Focused and deterministic for analysis tasks',
    icon: 'chart-bar',
    parameters: {
      temperature: 0.1,
      topP: 0.9,
      frequencyPenalty: 0,
      presencePenalty: 0,
    },
  },
  {
    id: 'conversational',
    name: 'Conversational',
    description: 'Natural dialogue with varied responses',
    icon: 'message-circle',
    parameters: {
      temperature: 0.8,
      topP: 0.95,
      frequencyPenalty: 0.5,
      presencePenalty: 0.5,
    },
  },
];

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