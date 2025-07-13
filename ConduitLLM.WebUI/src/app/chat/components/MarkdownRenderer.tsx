'use client';

import { Fragment } from 'react';
import { Text, Table, Anchor, List, Title, Divider, Blockquote } from '@mantine/core';
import { CodeBlock } from './CodeBlock';

interface MarkdownRendererProps {
  content: string;
}

// Simple markdown parser
function parseMarkdown(text: string): React.ReactNode[] {
  const elements: React.ReactNode[] = [];
  const lines = text.split('\n');
  let i = 0;
  let key = 0;

  while (i < lines.length) {
    const line = lines[i];

    // Code blocks
    if (line.startsWith('```')) {
      const language = line.slice(3).trim();
      const codeLines: string[] = [];
      i++;
      while (i < lines.length && !lines[i].startsWith('```')) {
        codeLines.push(lines[i]);
        i++;
      }
      elements.push(
        <CodeBlock
          key={key++}
          code={codeLines.join('\n')}
          language={language}
        />
      );
      i++;
      continue;
    }

    // Headers
    const headerMatch = line.match(/^(#{1,6})\s+(.+)$/);
    if (headerMatch) {
      const level = headerMatch[1].length;
      elements.push(
        <Title key={key++} order={level as any} mt="md" mb="xs">
          {parseInline(headerMatch[2])}
        </Title>
      );
      i++;
      continue;
    }

    // Horizontal rule
    if (line.match(/^---+$/)) {
      elements.push(<Divider key={key++} my="md" />);
      i++;
      continue;
    }

    // Blockquote
    if (line.startsWith('>')) {
      const quoteLines: string[] = [];
      while (i < lines.length && lines[i].startsWith('>')) {
        quoteLines.push(lines[i].slice(1).trim());
        i++;
      }
      elements.push(
        <Blockquote key={key++} my="sm">
          {quoteLines.map((l, idx) => (
            <Text key={idx}>{parseInline(l)}</Text>
          ))}
        </Blockquote>
      );
      continue;
    }

    // Lists
    if (line.match(/^[\*\-\+]\s+/) || line.match(/^\d+\.\s+/)) {
      const listItems: string[] = [];
      const isOrdered = /^\d+\.\s+/.test(line);
      
      while (i < lines.length && (lines[i].match(/^[\*\-\+]\s+/) || lines[i].match(/^\d+\.\s+/))) {
        listItems.push(lines[i].replace(/^[\*\-\+]\s+/, '').replace(/^\d+\.\s+/, ''));
        i++;
      }
      
      elements.push(
        <List key={key++} type={isOrdered ? 'ordered' : 'unordered'} my="sm">
          {listItems.map((item, idx) => (
            <List.Item key={idx}>{parseInline(item)}</List.Item>
          ))}
        </List>
      );
      continue;
    }

    // Tables (simple implementation)
    if (line.includes('|') && i + 1 < lines.length && lines[i + 1].includes('---')) {
      const headers = line.split('|').map(h => h.trim()).filter(Boolean);
      i += 2; // Skip separator line
      const rows: string[][] = [];
      
      while (i < lines.length && lines[i].includes('|')) {
        rows.push(lines[i].split('|').map(c => c.trim()).filter(Boolean));
        i++;
      }
      
      elements.push(
        <Table key={key++} my="sm" withTableBorder withColumnBorders>
          <Table.Thead>
            <Table.Tr>
              {headers.map((h, idx) => (
                <Table.Th key={idx}>{parseInline(h)}</Table.Th>
              ))}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {rows.map((row, rowIdx) => (
              <Table.Tr key={rowIdx}>
                {row.map((cell, cellIdx) => (
                  <Table.Td key={cellIdx}>{parseInline(cell)}</Table.Td>
                ))}
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      );
      continue;
    }

    // Regular paragraphs
    if (line.trim()) {
      elements.push(
        <Text key={key++} my="xs">
          {parseInline(line)}
        </Text>
      );
    }

    i++;
  }

  return elements;
}

// Parse inline markdown elements
function parseInline(text: string): React.ReactNode {
  const elements: React.ReactNode[] = [];
  let lastIndex = 0;

  // Combined regex for all inline patterns
  const patterns = [
    { regex: /\*\*([^*]+)\*\*/g, render: (m: RegExpExecArray) => <strong key={m.index}>{m[1]}</strong> },
    { regex: /\*([^*]+)\*/g, render: (m: RegExpExecArray) => <em key={m.index}>{m[1]}</em> },
    { regex: /`([^`]+)`/g, render: (m: RegExpExecArray) => <code key={m.index} style={{ 
      backgroundColor: 'var(--mantine-color-gray-2)', 
      padding: '2px 4px', 
      borderRadius: '3px',
      fontFamily: 'monospace',
      fontSize: '0.875em'
    }}>{m[1]}</code> },
    { regex: /\[([^\]]+)\]\(([^)]+)\)/g, render: (m: RegExpExecArray) => (
      <Anchor key={m.index} href={m[2]} target="_blank" rel="noopener noreferrer">
        {m[1]}
      </Anchor>
    )},
    // Basic math support (inline)
    { regex: /\$([^$]+)\$/g, render: (m: RegExpExecArray) => (
      <code key={m.index} style={{ 
        backgroundColor: 'var(--mantine-color-gray-1)', 
        padding: '2px 4px', 
        borderRadius: '3px',
        fontFamily: 'monospace',
        fontStyle: 'italic'
      }}>
        {m[1]}
      </code>
    )},
  ];

  // Find all matches
  const matches: Array<{ index: number; length: number; element: React.ReactNode }> = [];
  
  patterns.forEach(({ regex, render }) => {
    let match;
    regex.lastIndex = 0;
    while ((match = regex.exec(text)) !== null) {
      matches.push({
        index: match.index,
        length: match[0].length,
        element: render(match),
      });
    }
  });

  // Sort matches by index
  matches.sort((a, b) => a.index - b.index);

  // Build result
  matches.forEach((match) => {
    if (match.index > lastIndex) {
      elements.push(text.substring(lastIndex, match.index));
    }
    elements.push(match.element);
    lastIndex = match.index + match.length;
  });

  if (lastIndex < text.length) {
    elements.push(text.substring(lastIndex));
  }

  return elements.length > 0 ? <Fragment>{elements}</Fragment> : text;
}

export function MarkdownRenderer({ content }: MarkdownRendererProps) {
  const elements = parseMarkdown(content);

  return (
    <div className="markdown-content">
      {elements}
      <style jsx global>{`
        .markdown-content > *:first-child {
          margin-top: 0;
        }
        
        .markdown-content > *:last-child {
          margin-bottom: 0;
        }
      `}</style>
    </div>
  );
}