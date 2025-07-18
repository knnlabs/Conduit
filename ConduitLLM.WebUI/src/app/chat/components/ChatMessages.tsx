import { ScrollArea, Stack, Text, Group, Badge, Paper, Code } from '@mantine/core';
import { IconUser, IconRobot, IconClock, IconBolt } from '@tabler/icons-react';
import { ChatMessage } from '../types';
import { useEffect, useRef } from 'react';
import ReactMarkdown from 'react-markdown';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/cjs/styles/prism';
import { ImagePreview } from './ImagePreview';

interface ChatMessagesProps {
  messages: ChatMessage[];
  streamingContent?: string;
  tokensPerSecond?: number | null;
}

export function ChatMessages({ messages, streamingContent, tokensPerSecond }: ChatMessagesProps) {
  const scrollAreaRef = useRef<HTMLDivElement>(null);
  const lastMessageRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (lastMessageRef.current) {
      lastMessageRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages, streamingContent]);

  const renderMessage = (message: ChatMessage, isStreaming = false) => {
    const isUser = message.role === 'user';
    const content = isStreaming ? streamingContent : message.content;

    return (
      <Paper
        key={message.id}
        p="md"
        radius="md"
        className={isUser ? 'chat-message-user' : 'chat-message-assistant'}
        style={{
          alignSelf: isUser ? 'flex-end' : 'flex-start',
          maxWidth: '80%',
        }}
      >
        <Stack gap="xs">
          <Group justify="space-between" wrap="nowrap">
            <Group gap="xs">
              {isUser ? (
                <IconUser size={16} />
              ) : (
                <IconRobot size={16} />
              )}
              <Text fw={600} size="sm">
                {isUser ? 'You' : message.model ?? 'Assistant'}
              </Text>
            </Group>
            
            {!isUser && (message.metadata ?? (isStreaming && tokensPerSecond)) && (
              <Group gap="xs">
                {message.metadata?.tokensUsed !== null && message.metadata?.tokensUsed !== undefined && (
                  <Badge size="xs" variant="light">
                    {message.metadata.tokensUsed} tokens
                  </Badge>
                )}
                {(message.metadata?.tokensPerSecond ?? (isStreaming && tokensPerSecond)) && (
                  <Badge size="xs" variant="light" color="green">
                    <Group gap={4}>
                      <IconBolt size={12} />
                      {(message.metadata?.tokensPerSecond ?? tokensPerSecond)?.toFixed(1)} t/s
                    </Group>
                  </Badge>
                )}
                {message.metadata?.latency !== null && message.metadata?.latency !== undefined && (
                  <Badge size="xs" variant="light" color="blue">
                    <Group gap={4}>
                      <IconClock size={12} />
                      {(message.metadata.latency / 1000).toFixed(1)}s
                    </Group>
                  </Badge>
                )}
              </Group>
            )}
          </Group>
          
          {message.images && message.images.length > 0 && (
            <ImagePreview images={message.images} compact />
          )}
          
          {message.functionCall && (
            <Paper p="xs" radius="sm" withBorder>
              <Text size="xs" fw={600} mb={4}>Function Call:</Text>
              <Text size="xs" c="blue" fw={500}>{message.functionCall.name}</Text>
              <Code block mt={4}>
                {message.functionCall.arguments}
              </Code>
            </Paper>
          )}
          
          {message.toolCalls && message.toolCalls.length > 0 && (
            <Stack gap="xs">
              <Text size="xs" fw={600}>Tool Calls:</Text>
              {message.toolCalls.map((tool) => (
                <Paper key={tool.id || tool.function.name} p="xs" radius="sm" withBorder>
                  <Text size="xs" c="green" fw={500}>{tool.function.name}</Text>
                  <Code block mt={4} style={{ fontSize: '0.75rem' }}>
                    {tool.function.arguments}
                  </Code>
                </Paper>
              ))}
            </Stack>
          )}
          
          <div className="markdown-content">
            <ReactMarkdown
              components={{
                code({ className, children, ...props }) {
                  const match = /language-(\w+)/.exec(className ?? '');
                  const inline = !className;
                  
                  const getChildrenText = (node: React.ReactNode): string => {
                    if (typeof node === 'string') return node;
                    if (typeof node === 'number') return node.toString();
                    if (Array.isArray(node)) return node.map(getChildrenText).join('');
                    return '';
                  };
                  
                  const childText = getChildrenText(children);
                  
                  return !inline && match ? (
                    <SyntaxHighlighter
                      style={vscDarkPlus}
                      language={match[1]}
                      PreTag="div"
                      {...(props as Record<string, unknown>)}
                    >
                      {childText.replace(/\n$/, '')}
                    </SyntaxHighlighter>
                  ) : (
                    <code className={className} {...props}>
                      {childText}
                    </code>
                  );
                },
              }}
            >
              {content ?? ''}
            </ReactMarkdown>
          </div>
        </Stack>
      </Paper>
    );
  };

  return (
    <ScrollArea 
      style={{ flex: 1 }} 
      viewportRef={scrollAreaRef}
      type="auto"
    >
      <Stack gap="md" p="xs">
        {messages.map((message) => renderMessage(message))}
        {streamingContent && renderMessage(
          {
            id: 'streaming',
            role: 'assistant',
            content: '',
            timestamp: new Date(),
          },
          true
        )}
        <div ref={lastMessageRef} />
      </Stack>
    </ScrollArea>
  );
}