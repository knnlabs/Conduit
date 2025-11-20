import { ScrollArea, Stack, Text, Group, Badge, Paper, Code, Collapse, ActionIcon, Alert, HoverCard, CopyButton, Tooltip } from '@mantine/core';
import { IconUser, IconRobot, IconClock, IconBolt, IconAlertCircle, IconNetwork, IconLock, IconSearch, IconAlertTriangle, IconChevronDown, IconChevronUp, IconInfoCircle, IconCopy, IconCheck, IconCode, IconEye } from '@tabler/icons-react';
import { ChatMessage, ChatErrorType } from '../types';
import React, { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/cjs/styles/prism';
import { ImagePreview } from './ImagePreview';
import { processStructuredContent, getBlockQuoteMetadata, cleanBlockQuoteContent } from '@knn_labs/conduit-core-client';

interface ChatMessagesProps {
  messages: ChatMessage[];
  isLoading?: boolean;
  streamingContent?: string;
  streamingChannel?: string | null;
  tokensPerSecond?: number | null;
  reasoningExpanded?: boolean;
}

// Helper function to get error type styling
function getErrorTypeConfig(type: ChatErrorType) {
  switch (type) {
    case 'rate_limit':
      return { icon: IconClock, color: 'orange', label: 'Rate Limit' };
    case 'model_not_found':
      return { icon: IconSearch, color: 'blue', label: 'Model Not Found' };
    case 'auth_error':
      return { icon: IconLock, color: 'red', label: 'Authentication Error' };
    case 'network_error':
      return { icon: IconNetwork, color: 'gray', label: 'Network Error' };
    case 'server_error':
    default:
      return { icon: IconAlertTriangle, color: 'red', label: 'Server Error' };
  }
}

export function ChatMessages({ messages, isLoading, streamingContent, streamingChannel, tokensPerSecond, reasoningExpanded = true }: ChatMessagesProps) {
  const scrollAreaRef = useRef<HTMLDivElement>(null);
  const lastMessageRef = useRef<HTMLDivElement>(null);
  const [expandedErrors, setExpandedErrors] = useState<Set<string>>(new Set());
  const [expandedReasoning, setExpandedReasoning] = useState<Set<string>>(new Set());
  const [rawViewMessages, setRawViewMessages] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (lastMessageRef.current) {
      lastMessageRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages, streamingContent]);

  const toggleErrorDetails = (messageId: string) => {
    setExpandedErrors(prev => {
      const next = new Set(prev);
      if (next.has(messageId)) {
        next.delete(messageId);
      } else {
        next.add(messageId);
      }
      return next;
    });
  };

  const toggleRawView = (messageId: string) => {
    setRawViewMessages(prev => {
      const next = new Set(prev);
      if (next.has(messageId)) {
        next.delete(messageId);
      } else {
        next.add(messageId);
      }
      return next;
    });
  };

  // Component for collapsible thinking blocks
  const CollapsibleThinking = ({ content, icon, title }: { content: string; icon: string; title: string }) => {
    const [isOpen, setIsOpen] = useState(false);
    
    return (
      <Paper 
        p="sm" 
        radius="md" 
        withBorder 
        style={{ 
          backgroundColor: 'var(--mantine-color-gray-light)',
          cursor: 'pointer',
          transition: 'all 0.2s ease',
          userSelect: 'none'
        }}
        className="thinking-block"
        onClick={() => setIsOpen(!isOpen)}
      >
        <Group 
          gap="xs" 
          wrap="nowrap"
        >
          <ActionIcon 
            variant="subtle" 
            size="sm"
            style={{ pointerEvents: 'none' }}
          >
            {isOpen ? <IconChevronUp size={14} /> : <IconChevronDown size={14} />}
          </ActionIcon>
          <Text size="sm" fw={500} style={{ flex: 1 }}>
            {icon} {title}
          </Text>
          <Text size="xs" c="dimmed">
            {isOpen ? 'Click to collapse' : 'Click to expand'}
          </Text>
        </Group>
        <Collapse in={isOpen}>
          <div style={{ marginTop: '0.5rem', paddingLeft: '1.5rem' }}>
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
          </div>
        </Collapse>
      </Paper>
    );
  };

  const renderMessage = (message: ChatMessage, isStreaming = false) => {
    const isUser = message.role === 'user';
    const content = message.content;  // Just use the content from the message
    const hasError = message.error && !isUser;
    const errorConfig = hasError && message.error ? getErrorTypeConfig(message.error.type) : null;
    const isExpanded = expandedErrors.has(message.id);
    const isRawView = rawViewMessages.has(message.id);
    
    // Check if this message has reasoning in metadata
    const hasReasoning = !isUser && message.metadata?.hasReasoning && message.metadata?.reasoning;
    const reasoningText = hasReasoning ? message.metadata?.reasoning : null;

    // For error messages, render special error UI
    if (hasError && errorConfig && message.error) {
      const Icon = errorConfig.icon;
      return (
        <Paper
          key={message.id}
          p="md"
          radius="md"
          withBorder
          className={`chat-message-error chat-message-error-${message.error.type.replace('_', '-')}`}
          style={{
            alignSelf: 'flex-start',
            maxWidth: '80%',
          }}
        >
          <Stack gap="sm">
            {/* Error header with icon and type */}
            <Group justify="space-between" wrap="nowrap">
              <Group gap="sm">
                <Icon size={20} color={`var(--mantine-color-${errorConfig.color}-6)`} />
                <Badge color={errorConfig.color} variant="light">
                  {errorConfig.label}
                </Badge>
              </Group>
              {message.error.retryAfter && (
                <Badge size="sm" variant="light" color="gray">
                  Retry after {message.error.retryAfter}s
                </Badge>
              )}
            </Group>

            {/* User-friendly error message */}
            <Text size="sm">
              {content?.replace('Error: ', '')}
            </Text>

            {/* Suggestions if available */}
            {message.error.suggestions && message.error.suggestions.length > 0 && (
              <Alert icon={<IconAlertCircle size={16} />} color={errorConfig.color} variant="light">
                <Stack gap="xs">
                  <Text size="sm" fw={500}>Suggestions:</Text>
                  {message.error.suggestions.map((suggestion) => (
                    <Text key={suggestion} size="xs">• {suggestion}</Text>
                  ))}
                </Stack>
              </Alert>
            )}

            {/* Technical details (expandable) */}
            {(message.error.technical ?? message.error.code ?? message.error.statusCode) && (
              <>
                <Group gap="xs">
                  <ActionIcon
                    variant="subtle"
                    size="sm"
                    onClick={() => toggleErrorDetails(message.id)}
                  >
                    {isExpanded ? <IconChevronUp size={14} /> : <IconChevronDown size={14} />}
                  </ActionIcon>
                  <Text size="xs" c="dimmed">Technical Details</Text>
                </Group>
                <Collapse in={isExpanded}>
                  <Paper p="sm" radius="sm" withBorder className="error-details-box">
                    <Stack gap="xs">
                      {message.error.statusCode && (
                        <Text size="xs">
                          <Text span fw={500}>HTTP Status:</Text> {message.error.statusCode}
                        </Text>
                      )}
                      {message.error.code && (
                        <Text size="xs">
                          <Text span fw={500}>Error Code:</Text> {message.error.code}
                        </Text>
                      )}
                      {message.error.technical && (
                        <Code block style={{ fontSize: '0.75rem' }}>
                          {message.error.technical}
                        </Code>
                      )}
                    </Stack>
                  </Paper>
                </Collapse>
              </>
            )}
          </Stack>
        </Paper>
      );
    }

    // Regular message rendering
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
            <Group gap="xs" wrap="wrap">
              {isUser ? (
                <IconUser size={16} />
              ) : (
                <IconRobot size={16} />
              )}
              <Text fw={600} size="sm">
                {isUser ? 'You' : message.model ?? 'Assistant'}
              </Text>

              {/* Function indicators for user messages */}
              {isUser && message.metadata?.functionNames && message.metadata.functionNames.length > 0 && (
                <Group gap={4}>
                  {message.metadata.functionNames.map((name: string) => (
                    <Badge key={name} size="xs" variant="light" color="violet">
                      {name}
                    </Badge>
                  ))}
                </Group>
              )}
            </Group>

            {/* User message metadata (code icon) */}
            {isUser && (
              <Group gap="xs">
                {/* Toggle Raw View Button for user messages */}
                <Tooltip label={isRawView ? 'Show formatted view' : 'Show raw request'} withArrow>
                  <ActionIcon
                    variant="subtle"
                    size="sm"
                    onClick={() => toggleRawView(message.id)}
                    color={isRawView ? 'blue' : 'gray'}
                  >
                    {isRawView ? <IconEye size={16} /> : <IconCode size={16} />}
                  </ActionIcon>
                </Tooltip>
              </Group>
            )}

            {!isUser && (message.metadata ?? (isStreaming && tokensPerSecond)) && (
              <Group gap="xs">
                {/* Toggle Raw View Button */}
                {message.metadata && !isStreaming && (
                  <Tooltip label={isRawView ? 'Show formatted view' : 'Show raw response'} withArrow>
                    <ActionIcon 
                      variant="subtle" 
                      size="sm" 
                      onClick={() => toggleRawView(message.id)}
                      color={isRawView ? 'blue' : 'gray'}
                    >
                      {isRawView ? <IconEye size={16} /> : <IconCode size={16} />}
                    </ActionIcon>
                  </Tooltip>
                )}
                {message.metadata?.tokensUsed !== null && message.metadata?.tokensUsed !== undefined && message.metadata.tokensUsed > 0 && (
                  <Badge size="xs" variant="light">
                    {message.metadata.tokensUsed} tokens
                  </Badge>
                )}
                {(() => {
                  const tps = message.metadata?.tokensPerSecond ?? (isStreaming ? tokensPerSecond : null);
                  return tps !== null && tps !== undefined && tps > 0 ? (
                    <Badge size="xs" variant="light" color="green">
                      <Group gap={4}>
                        <IconBolt size={12} />
                        {tps.toFixed(1)} t/s
                      </Group>
                    </Badge>
                  ) : null;
                })()}
                {message.metadata?.latency !== null && message.metadata?.latency !== undefined && message.metadata.latency > 0 && (
                  <Badge size="xs" variant="light" color="blue">
                    <Group gap={4}>
                      <IconClock size={12} />
                      {(message.metadata.latency / 1000).toFixed(1)}s
                    </Group>
                  </Badge>
                )}
                {/* Metadata hover card */}
                {(message.metadata?.provider ?? message.metadata?.model ?? message.metadata?.promptTokens ?? message.metadata?.completionTokens) && (
                  <HoverCard width={280} shadow="md" withArrow>
                    <HoverCard.Target>
                      <ActionIcon variant="subtle" size="sm" color="gray">
                        <IconInfoCircle size={16} />
                      </ActionIcon>
                    </HoverCard.Target>
                    <HoverCard.Dropdown>
                      <Stack gap="xs">
                        <Text size="sm" fw={600}>Response Details</Text>
                        {message.metadata.streaming !== undefined && (
                          <Group gap="xs">
                            <Text size="xs" c="dimmed">Response Type:</Text>
                            <Text size="xs">{message.metadata.streaming ? 'SSE (Streaming)' : 'JSON (Complete)'}</Text>
                          </Group>
                        )}
                        {message.metadata.provider && (
                          <Group gap="xs">
                            <Text size="xs" c="dimmed">Provider:</Text>
                            <Text size="xs">{message.metadata.provider}</Text>
                          </Group>
                        )}
                        {message.metadata.model && (
                          <Group gap="xs">
                            <Text size="xs" c="dimmed">Model:</Text>
                            <Text size="xs">{message.metadata.model}</Text>
                          </Group>
                        )}
                        {message.metadata.promptTokens !== undefined && (
                          <Group gap="xs">
                            <Text size="xs" c="dimmed">Prompt Tokens:</Text>
                            <Text size="xs">{message.metadata.promptTokens}</Text>
                          </Group>
                        )}
                        {message.metadata.completionTokens !== undefined && (
                          <Group gap="xs">
                            <Text size="xs" c="dimmed">Completion Tokens:</Text>
                            <Text size="xs">{message.metadata.completionTokens}</Text>
                          </Group>
                        )}
                      </Stack>
                    </HoverCard.Dropdown>
                  </HoverCard>
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
          
          {/* Show reasoning if present - collapsible (only in normal view) */}
          {reasoningText && !isRawView && (
            <Paper 
              p="sm" 
              radius="md" 
              withBorder 
              style={{ 
                backgroundColor: 'var(--mantine-color-gray-light)',
                marginBottom: '0.75rem',
                cursor: 'pointer',
                transition: 'all 0.2s ease',
                userSelect: 'none'
              }}
              className="reasoning-block"
              onClick={() => {
                const newExpanded = new Set(expandedReasoning);
                if (newExpanded.has(message.id)) {
                  newExpanded.delete(message.id);
                } else {
                  newExpanded.add(message.id);
                }
                setExpandedReasoning(newExpanded);
              }}
            >
              <Group 
                gap="xs" 
                wrap="nowrap"
              >
                <ActionIcon 
                  variant="subtle" 
                  size="sm"
                  style={{ pointerEvents: 'none' }}
                >
                  {/* Check if this message's reasoning is expanded */}
                  {(() => {
                    const isExpanded = expandedReasoning.has(message.id) 
                      ? !reasoningExpanded  // If in set, opposite of default
                      : reasoningExpanded;  // Otherwise, use default
                    return isExpanded ? <IconChevronUp size={14} /> : <IconChevronDown size={14} />;
                  })()}
                </ActionIcon>
                <Text size="sm" fw={500} style={{ flex: 1 }}>
                  🧠 Reasoning
                </Text>
                <Text size="xs" c="dimmed">
                  {(() => {
                    const isExpanded = expandedReasoning.has(message.id) 
                      ? !reasoningExpanded  // If in set, opposite of default
                      : reasoningExpanded;  // Otherwise, use default
                    return isExpanded ? 'Click to collapse' : 'Click to expand';
                  })()}
                </Text>
              </Group>
              <Collapse in={(() => {
                const isExpanded = expandedReasoning.has(message.id) 
                  ? !reasoningExpanded  // If in set, opposite of default
                  : reasoningExpanded;  // Otherwise, use default
                return isExpanded;
              })()}>
                <div className="reasoning-content markdown-content" style={{ marginTop: '0.5rem', paddingLeft: '1.5rem' }}>
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>{reasoningText}</ReactMarkdown>
                </div>
              </Collapse>
            </Paper>
          )}
          
          {/* Conditionally render normal view or raw JSON view */}
          {(() => {
            // Raw view for user messages
            if (isRawView && !isStreaming && isUser) {
              return (
                <Stack gap="md">
                  {/* Message Data Section */}
                  <div>
                    <Text size="xs" fw={600} mb={4} c="dimmed">Message Data:</Text>
                    <div style={{ maxHeight: '300px', overflow: 'auto' }}>
                      <SyntaxHighlighter
                        language="json"
                        style={vscDarkPlus}
                        customStyle={{
                          margin: 0,
                          fontSize: '0.85rem',
                          borderRadius: '4px'
                        }}
                      >
                        {JSON.stringify({
                          id: message.id,
                          role: message.role,
                          timestamp: message.timestamp,
                          content: message.content,
                          ...(message.images && message.images.length > 0 && { images: message.images }),
                          ...(message.metadata?.functionIds && { function_ids: message.metadata.functionIds }),
                          ...(message.metadata?.functionNames && { function_names: message.metadata.functionNames })
                        }, null, 2)}
                      </SyntaxHighlighter>
                    </div>
                  </div>

                  {/* API Request Section */}
                  {message.metadata?.apiRequest && (
                    <div>
                      <Text size="xs" fw={600} mb={4} c="dimmed">API Request Sent to Conduit:</Text>
                      <div style={{ maxHeight: '400px', overflow: 'auto' }}>
                        <SyntaxHighlighter
                          language="json"
                          style={vscDarkPlus}
                          customStyle={{
                            margin: 0,
                            fontSize: '0.85rem',
                            borderRadius: '4px'
                          }}
                        >
                          {JSON.stringify(message.metadata.apiRequest, null, 2)}
                        </SyntaxHighlighter>
                      </div>
                    </div>
                  )}
                </Stack>
              );
            }

            // Raw view for assistant messages
            if (isRawView && !isStreaming && !isUser) {
              return (
                <div style={{ maxHeight: '400px', overflow: 'auto' }}>
                  <SyntaxHighlighter
                    language="json"
                    style={vscDarkPlus}
                    customStyle={{
                      margin: 0,
                      fontSize: '0.85rem',
                      borderRadius: '4px'
                    }}
                  >
                    {JSON.stringify({
                      id: message.id,
                      timestamp: message.timestamp,
                      model: message.model ?? message.metadata?.model,
                      content: message.content,
                      metadata: message.metadata ? {
                        ...(message.metadata.latency !== undefined && { latency_ms: message.metadata.latency }),
                        ...(message.metadata.timeToFirstToken !== undefined && { time_to_first_token_ms: message.metadata.timeToFirstToken }),
                        ...(message.metadata.tokensPerSecond !== undefined && { tokens_per_second: message.metadata.tokensPerSecond }),
                        ...(message.metadata.promptTokens !== undefined && { prompt_tokens: message.metadata.promptTokens }),
                        ...(message.metadata.completionTokens !== undefined && { completion_tokens: message.metadata.completionTokens }),
                        ...(message.metadata.tokensUsed !== undefined && { total_tokens: message.metadata.tokensUsed }),
                        ...(message.metadata.provider && { provider: message.metadata.provider }),
                        ...(message.metadata.model && { model: message.metadata.model }),
                        ...(message.metadata.streaming !== undefined && { streaming: message.metadata.streaming }),
                        ...(message.metadata.finishReason && { finish_reason: message.metadata.finishReason }),
                        ...(message.metadata.hasReasoning && { has_reasoning: message.metadata.hasReasoning }),
                        ...(message.metadata.reasoning && { reasoning: message.metadata.reasoning })
                      } : undefined,
                      ...(message.functionCall && { function_call: message.functionCall }),
                      ...(message.toolCalls && { tool_calls: message.toolCalls }),
                      ...(message.images && message.images.length > 0 && { images: message.images })
                    }, null, 2)}
                  </SyntaxHighlighter>
                </div>
              );
            }

            // Normal Markdown View
            return (
            <div className={`markdown-content ${isStreaming && streamingChannel === 'analysis' ? 'reasoning-content' : ''}`}>
              {/* JUST SHOW THE RAW CONTENT */}
              {isStreaming ? (
                <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit' }}>{content}</pre>
              ) : (
              <ReactMarkdown
              remarkPlugins={[remarkGfm]}
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
                blockquote({ children, ...props }) {
                  const getChildrenText = (node: React.ReactNode): string => {
                    if (typeof node === 'string') return node;
                    if (typeof node === 'number') return node.toString();
                    if (Array.isArray(node)) return node.map(getChildrenText).join('');
                    if (!node || typeof node !== 'object') return '';
                    
                    // Type guard for React element
                    if (React.isValidElement(node)) {
                      const element = node as React.ReactElement<{children?: React.ReactNode}>;
                      if (element.props && element.props.children !== undefined) {
                        return getChildrenText(element.props.children);
                      }
                    }
                    return '';
                  };
                  
                  const text = getChildrenText(children);
                  const metadata = getBlockQuoteMetadata(text);
                  
                  // Handle thinking blocks with collapsible UI
                  if (metadata.type === 'thinking') {
                    const cleanedContent = cleanBlockQuoteContent(text);
                    
                    return (
                      <CollapsibleThinking 
                        content={cleanedContent}
                        icon={metadata.icon}
                        title={metadata.title}
                      />
                    );
                  }
                  
                  // Handle warning blocks
                  if (metadata.type === 'warning') {
                    const cleanedContent = cleanBlockQuoteContent(text);
                    
                    return (
                      <Alert 
                        icon={<IconAlertTriangle size={16} />} 
                        color="orange" 
                        variant="light"
                        radius="md"
                      >
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{cleanedContent}</ReactMarkdown>
                      </Alert>
                    );
                  }
                  
                  // Handle summary blocks
                  if (metadata.type === 'summary') {
                    const cleanedContent = cleanBlockQuoteContent(text);
                    
                    return (
                      <Paper 
                        p="md" 
                        radius="md" 
                        withBorder 
                        style={{ 
                          backgroundColor: 'var(--mantine-color-blue-light)',
                          borderColor: 'var(--mantine-color-blue-6)'
                        }}
                      >
                        <Text size="sm" fw={600} mb="xs">
                          {metadata.icon} {metadata.title}
                        </Text>
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>{cleanedContent}</ReactMarkdown>
                      </Paper>
                    );
                  }
                  
                  // Default blockquote
                  return <blockquote {...props}>{children}</blockquote>;
                },
              }}
            >
              {processStructuredContent(content ?? '')}
              </ReactMarkdown>
              )}
            </div>
            );
          })()}
          
          {/* Copy button */}
          {content && (
            <Group justify="flex-end" mt="xs">
              <CopyButton value={(() => {
                if (!isRawView) {
                  return content;
                }
                if (isUser) {
                  return JSON.stringify({
                    message_data: {
                      id: message.id,
                      role: message.role,
                      timestamp: message.timestamp,
                      content: message.content,
                      images: message.images,
                      function_ids: message.metadata?.functionIds,
                      function_names: message.metadata?.functionNames
                    },
                    api_request: message.metadata?.apiRequest
                  }, null, 2);
                }
                return JSON.stringify({
                  id: message.id,
                  timestamp: message.timestamp,
                  model: message.model ?? message.metadata?.model,
                  content: message.content,
                  metadata: message.metadata,
                  function_call: message.functionCall,
                  tool_calls: message.toolCalls,
                  images: message.images
                }, null, 2);
              })()} timeout={2000}>
                {({ copied, copy }) => (
                  <Tooltip label={(() => {
                    if (copied) return 'Copied!';
                    return isRawView ? 'Copy JSON' : 'Copy message';
                  })()} withArrow position="left">
                    <ActionIcon
                      color={copied ? 'teal' : 'gray'}
                      onClick={copy}
                      variant="subtle"
                      size="sm"
                    >
                      {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
                    </ActionIcon>
                  </Tooltip>
                )}
              </CopyButton>
            </Group>
          )}
        </Stack>
      </Paper>
    );
  };

  return (
    <ScrollArea 
      style={{ height: '100%', flex: 1 }} 
      viewportRef={scrollAreaRef}
      type="auto"
      scrollbarSize={8}
      scrollHideDelay={800}
    >
      <Stack gap="md" p="xs">
        {messages.map((message) => renderMessage(message))}
        {/* eslint-disable-next-line @typescript-eslint/prefer-nullish-coalescing */}
        {(isLoading || streamingContent) && (
          <Paper
            p="md"
            radius="md"
            className="chat-message-assistant"
            style={{
              alignSelf: 'flex-start',
              maxWidth: '80%',
            }}
          >
            <Stack gap="xs">
              <Group gap="xs">
                <IconRobot size={16} />
                <Text fw={600} size="sm">
                  {streamingContent ? 'Streaming...' : 'Assistant'}
                </Text>
              </Group>
              {streamingContent ? (
                <div className="markdown-content">
                  <ReactMarkdown
                    remarkPlugins={[remarkGfm]}
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
                    {streamingContent}
                  </ReactMarkdown>
                </div>
              ) : (
                <Group gap={4}>
                  <span className="loading-dots">
                    <span>.</span>
                    <span>.</span>
                    <span>.</span>
                  </span>
                </Group>
              )}
            </Stack>
          </Paper>
        )}
        <div ref={lastMessageRef} />
      </Stack>
    </ScrollArea>
  );
}