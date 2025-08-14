'use client';

import { useState } from 'react';
import { 
  Paper, 
  Group, 
  ActionIcon, 
  CopyButton, 
  Tooltip, 
  Text,
  ScrollArea,
  Box
} from '@mantine/core';
import { IconCheck, IconCopy, IconTerminal2 } from '@tabler/icons-react';

interface CodeBlockProps {
  code: string;
  language?: string;
  filename?: string;
}

// Basic syntax highlighting patterns
const SYNTAX_PATTERNS: Record<string, Array<{ pattern: RegExp; className: string }>> = {
  javascript: [
    { pattern: /\b(const|let|var|function|return|if|else|for|while|class|import|export|from|async|await)\b/g, className: 'keyword' },
    { pattern: /(["'`])(?:(?=(\\?))\2.)*?\1/g, className: 'string' },
    { pattern: /\/\/.*$/gm, className: 'comment' },
    { pattern: /\/\*[\s\S]*?\*\//g, className: 'comment' },
    { pattern: /\b(\d+)\b/g, className: 'number' },
  ],
  typescript: [
    { pattern: /\b(const|let|var|function|return|if|else|for|while|class|import|export|from|async|await|type|interface|enum)\b/g, className: 'keyword' },
    { pattern: /(["'`])(?:(?=(\\?))\2.)*?\1/g, className: 'string' },
    { pattern: /\/\/.*$/gm, className: 'comment' },
    { pattern: /\/\*[\s\S]*?\*\//g, className: 'comment' },
    { pattern: /\b(\d+)\b/g, className: 'number' },
    { pattern: /\b(string|number|boolean|any|void|never|unknown)\b/g, className: 'type' },
  ],
  python: [
    { pattern: /\b(def|class|import|from|return|if|else|elif|for|while|with|as|try|except|finally|pass|break|continue|async|await)\b/g, className: 'keyword' },
    { pattern: /(["'])(?:(?=(\\?))\2.)*?\1/g, className: 'string' },
    { pattern: /#.*$/gm, className: 'comment' },
    { pattern: /\b(\d+)\b/g, className: 'number' },
    { pattern: /\b(True|False|None)\b/g, className: 'constant' },
  ],
  json: [
    { pattern: /(["'])(?:(?=(\\?))\2.)*?\1(?=\s*:)/g, className: 'property' },
    { pattern: /(["'])(?:(?=(\\?))\2.)*?\1/g, className: 'string' },
    { pattern: /\b(\d+)\b/g, className: 'number' },
    { pattern: /\b(true|false|null)\b/g, className: 'constant' },
  ],
};

function highlightCode(code: string, language?: string): string {
  if (!language || !SYNTAX_PATTERNS[language]) {
    return escapeHtml(code);
  }

  let highlighted = escapeHtml(code);
  const patterns = SYNTAX_PATTERNS[language];

  // Apply syntax highlighting
  patterns.forEach(({ pattern, className }) => {
    highlighted = highlighted.replace(pattern, (match) => {
      return `<span class="syntax-${className}">${match}</span>`;
    });
  });

  return highlighted;
}

function escapeHtml(text: string): string {
  const map: Record<string, string> = {
    ['&']: '&amp;',
    ['<']: '&lt;',
    ['>']: '&gt;',
    ['"']: '&quot;',
    ["'"]: '&#39;',
  };
  return text.replace(/[&<>"']/g, (m) => map[m]);
}

export function CodeBlock({ code, language, filename }: CodeBlockProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const lines = code.split('\n');
  const shouldTruncate = lines.length > 20;
  const displayCode = shouldTruncate && !isExpanded ? lines.slice(0, 20).join('\n') : code;
  const highlightedCode = highlightCode(displayCode, language);

  return (
    <Paper
      withBorder
      radius="sm"
      className="code-block"
      style={{ overflow: 'hidden' }}
    >
      <Group justify="space-between" p="xs" className="code-header">
        <Group gap="xs">
          <IconTerminal2 size={16} />
          {filename && <Text size="sm" c="dimmed">{filename}</Text>}
          {language && !filename && <Text size="sm" c="dimmed">{language}</Text>}
        </Group>
        <CopyButton value={code} timeout={2000}>
          {({ copied, copy }) => (
            <Tooltip label={copied ? 'Copied!' : 'Copy code'} position="left">
              <ActionIcon
                color={copied ? 'teal' : 'gray'}
                variant="subtle"
                onClick={copy}
                size="sm"
              >
                {copied ? <IconCheck size={16} /> : <IconCopy size={16} />}
              </ActionIcon>
            </Tooltip>
          )}
        </CopyButton>
      </Group>

      <ScrollArea.Autosize mah={500} type="auto">
        <Box p="sm" className="code-content">
          <pre style={{ margin: 0, fontSize: '0.875rem' }}>
            <code
              dangerouslySetInnerHTML={{ ['__html']: highlightedCode }}
              style={{ fontFamily: 'monospace' }}
            />
          </pre>
        </Box>
      </ScrollArea.Autosize>

      {shouldTruncate && (
        <Box p="xs" className="code-footer">
          <Text
            size="sm"
            c="blue"
            style={{ cursor: 'pointer' }}
            onClick={() => setIsExpanded(!isExpanded)}
          >
            {isExpanded ? 'Show less' : `Show ${lines.length - 20} more lines`}
          </Text>
        </Box>
      )}

      <style jsx global>{`
        .code-block {
          background-color: var(--mantine-color-gray-0);
        }
        
        @media (prefers-color-scheme: dark) {
          .code-block {
            background-color: var(--mantine-color-dark-7);
          }
        }
        
        .code-header {
          background-color: var(--mantine-color-gray-1);
          border-bottom: 1px solid var(--mantine-color-gray-3);
        }
        
        @media (prefers-color-scheme: dark) {
          .code-header {
            background-color: var(--mantine-color-dark-6);
            border-bottom-color: var(--mantine-color-dark-4);
          }
        }
        
        .code-content {
          font-size: 0.875rem;
          line-height: 1.5;
        }
        
        .code-footer {
          border-top: 1px solid var(--mantine-color-gray-3);
        }
        
        @media (prefers-color-scheme: dark) {
          .code-footer {
            border-top-color: var(--mantine-color-dark-4);
          }
        }
        
        /* Syntax highlighting colors */
        .syntax-keyword {
          color: #0969da;
          font-weight: 500;
        }
        
        .syntax-string {
          color: #032f62;
        }
        
        .syntax-comment {
          color: #6e7781;
          font-style: italic;
        }
        
        .syntax-number {
          color: #0550ae;
        }
        
        .syntax-constant {
          color: #cf222e;
        }
        
        .syntax-type {
          color: #953800;
        }
        
        .syntax-property {
          color: #0550ae;
        }
        
        @media (prefers-color-scheme: dark) {
          .syntax-keyword {
            color: #7ee787;
          }
          
          .syntax-string {
            color: #a5d6ff;
          }
          
          .syntax-comment {
            color: #8b949e;
          }
          
          .syntax-number {
            color: #79c0ff;
          }
          
          .syntax-constant {
            color: #ff7b72;
          }
          
          .syntax-type {
            color: #ffa657;
          }
          
          .syntax-property {
            color: #79c0ff;
          }
        }
      `}</style>
    </Paper>
  );
}