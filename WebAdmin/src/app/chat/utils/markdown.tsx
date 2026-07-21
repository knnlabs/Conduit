import React from 'react';
import { Paper, Text, Alert } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/cjs/styles/prism';
import ReactMarkdown, { type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { getBlockQuoteMetadata, cleanBlockQuoteContent } from '@/lib/gateway-api';

/**
 * Extract plain text from React children nodes.
 * Used by markdown renderers to get raw text content from nested elements.
 */
export function getChildrenText(node: React.ReactNode): string {
  if (typeof node === 'string') return node;
  if (typeof node === 'number') return node.toString();
  if (Array.isArray(node)) return node.map(getChildrenText).join('');
  if (!node || typeof node !== 'object') return '';

  if (React.isValidElement(node)) {
    const element = node as React.ReactElement<{ children?: React.ReactNode }>;
    if (element.props?.children !== undefined) {
      return getChildrenText(element.props.children);
    }
  }
  return '';
}

/**
 * Shared code block renderer for react-markdown.
 * Handles both inline code and fenced code blocks with syntax highlighting.
 */
function CodeRenderer({ className, children, ...props }: { className?: string; children?: React.ReactNode; [key: string]: unknown }) {
  const match = /language-(\w+)/.exec(className ?? '');
  const inline = !className;
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
}

/**
 * Minimal markdown components for streaming content (code blocks only).
 */
export const streamingMarkdownComponents: Components = {
  code: CodeRenderer as Components['code'],
};

/**
 * Creates full markdown components including blockquote handling for thinking/warning/summary blocks.
 */
export function createMessageMarkdownComponents(CollapsibleThinkingComponent: React.ComponentType<{ content: string; icon: string; title: string }>): Components {
  return {
    code: CodeRenderer as Components['code'],
    blockquote({ children, ...props }) {
      const text = getChildrenText(children);
      const metadata = getBlockQuoteMetadata(text);

      if (metadata.type === 'thinking') {
        const cleanedContent = cleanBlockQuoteContent(text);
        return (
          <CollapsibleThinkingComponent
            content={cleanedContent}
            icon={metadata.icon}
            title={metadata.title}
          />
        );
      }

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

      return <blockquote {...props}>{children}</blockquote>;
    },
  };
}
