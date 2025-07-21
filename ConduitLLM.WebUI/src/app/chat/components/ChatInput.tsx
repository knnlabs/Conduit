'use client';

import { 
  Textarea, 
  Button, 
  Group, 
  ActionIcon,
  Badge,
  Tooltip,
  Stack,
  Text,
  Paper,
  Modal,
  JsonInput,
  Collapse,
  Divider
} from '@mantine/core';
import { 
  IconSend, 
  IconPlayerStop,
  IconTool,
  IconMicrophone,
  IconX
} from '@tabler/icons-react';
import { useState, useRef, KeyboardEvent } from 'react';
import { ModelWithCapabilities, FunctionDefinition, ImageAttachment } from '../types';
import { useDisclosure } from '@mantine/hooks';
import { ImageUpload } from './ImageUpload';

interface ChatInputProps {
  onSendMessage: (message: string, images?: ImageAttachment[]) => void;
  isStreaming: boolean;
  onStopStreaming: () => void;
  disabled?: boolean;
  model?: ModelWithCapabilities;
}

export function ChatInput({ 
  onSendMessage, 
  isStreaming, 
  onStopStreaming, 
  disabled,
  model
}: ChatInputProps) {
  const [message, setMessage] = useState('');
  const [images, setImages] = useState<ImageAttachment[]>([]);
  const [functions, setFunctions] = useState<FunctionDefinition[]>([]);
  const [functionsEnabled, setFunctionsEnabled] = useState(false);
  const [functionModalOpened, { open: openFunctionModal, close: closeFunctionModal }] = useDisclosure(false);
  const [newFunctionJson, setNewFunctionJson] = useState('');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const handleSend = () => {
    if (message.trim() || images.length > 0) {
      onSendMessage(message.trim(), images);
      setMessage('');
      setImages([]);
    }
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const addFunction = () => {
    try {
      const parsed = JSON.parse(newFunctionJson) as FunctionDefinition;
      if (parsed.name) {
        setFunctions(prev => [...prev, parsed]);
        setNewFunctionJson('');
        closeFunctionModal();
      }
    } catch (e) {
      console.error('Invalid function JSON:', e);
    }
  };

  const removeFunction = (index: number) => {
    setFunctions(prev => prev.filter((fn, i) => i !== index));
  };

  const supportsFunctions = model?.supportsFunctionCalling ?? model?.supportsToolUsage;
  const supportsVision = model?.supportsVision;

  return (
    <Stack gap="xs">
      {supportsFunctions && (
        <Collapse in={functionsEnabled}>
          <Paper p="xs" withBorder radius="sm">
            <Group justify="space-between" mb="xs">
              <Text size="sm" fw={600}>Functions</Text>
              <Button size="xs" onClick={openFunctionModal}>
                Add Function
              </Button>
            </Group>
            
            {functions.length === 0 ? (
              <Text size="xs" c="dimmed">No functions defined</Text>
            ) : (
              <Group gap="xs">
                {functions.map((func) => (
                  <Badge
                    key={func.name}
                    variant="light"
                    rightSection={
                      <ActionIcon 
                        size="xs" 
                        variant="subtle" 
                        onClick={() => removeFunction(functions.indexOf(func))}
                      >
                        <IconX size={12} />
                      </ActionIcon>
                    }
                  >
                    {func.name}
                  </Badge>
                ))}
              </Group>
            )}
          </Paper>
        </Collapse>
      )}
      
      {supportsVision && images.length > 0 && (
        <>
          <ImageUpload
            images={images}
            onImagesChange={setImages}
            disabled={disabled ?? isStreaming}
          />
          <Divider />
        </>
      )}
      
      <Group gap="xs" align="flex-end">
        <Textarea
          ref={textareaRef}
          style={{ flex: 1 }}
          placeholder={isStreaming ? "Generating response..." : "Type your message..."}
          value={message}
          onChange={(e) => setMessage(e.currentTarget.value)}
          onKeyDown={handleKeyDown}
          minRows={1}
          maxRows={10}
          autosize
          disabled={disabled ?? isStreaming}
        />
        
        <Group gap="xs">
          {supportsFunctions && (
            <Tooltip label="Toggle function calling">
              <ActionIcon
                variant={functionsEnabled ? 'filled' : 'default'}
                size="lg"
                onClick={() => setFunctionsEnabled(!functionsEnabled)}
              >
                <IconTool size={20} />
              </ActionIcon>
            </Tooltip>
          )}
          
          {/* Audio input stub - TODO: Implement audio recording */}
          <Tooltip label="Audio input - Coming soon">
            <ActionIcon size="lg" variant="default" disabled>
              <IconMicrophone size={20} />
            </ActionIcon>
          </Tooltip>
          
          {isStreaming ? (
            <Button
              size="md"
              color="red"
              onClick={onStopStreaming}
              leftSection={<IconPlayerStop size={20} />}
            >
              Stop
            </Button>
          ) : (
            <Button
              size="md"
              onClick={handleSend}
              disabled={disabled ?? (!message.trim() && images.length === 0)}
              leftSection={<IconSend size={20} />}
            >
              Send
            </Button>
          )}
        </Group>
      </Group>
      
      {supportsVision && images.length === 0 && (
        <ImageUpload
          images={images}
          onImagesChange={setImages}
          disabled={disabled ?? isStreaming}
        />
      )}
      
      <Modal
        opened={functionModalOpened}
        onClose={closeFunctionModal}
        title="Add Function"
        size="lg"
      >
        <Stack>
          <Text size="sm" c="dimmed">
            Define a function that the AI can call. The function should follow the OpenAI function format.
          </Text>
          
          <JsonInput
            label="Function Definition"
            placeholder={JSON.stringify({
              name: "get_weather",
              description: "Get the current weather",
              parameters: {
                type: "object",
                properties: {
                  location: {
                    type: "string",
                    description: "The city and state"
                  }
                },
                required: ["location"]
              }
            }, null, 2)}
            value={newFunctionJson}
            onChange={setNewFunctionJson}
            minRows={10}
            formatOnBlur
            autosize
          />
          
          <Group justify="flex-end">
            <Button variant="default" onClick={closeFunctionModal}>
              Cancel
            </Button>
            <Button onClick={addFunction}>
              Add Function
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Stack>
  );
}