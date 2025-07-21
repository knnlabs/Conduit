import { 
  SimpleGrid, 
  Card, 
  Text, 
  Stack, 
  Group,
  Title,
  Container
} from '@mantine/core';
import { CONVERSATION_STARTERS, getCategoryIcon } from '../utils/presets';
import { ConversationStarter } from '../types';

interface ConversationStartersProps {
  onStarterClick: (prompt: string) => void;
}

export function ConversationStarters({ onStarterClick }: ConversationStartersProps) {
  const categories = Array.from(new Set(CONVERSATION_STARTERS.map(s => s.category)));

  const renderStarter = (starter: ConversationStarter) => {
    const Icon = getCategoryIcon(starter.category);
    
    return (
      <Card
        key={starter.id}
        shadow="sm"
        radius="md"
        withBorder
        style={{ cursor: 'pointer' }}
        onClick={() => onStarterClick(starter.prompt)}
        className="hover-card"
      >
        <Stack gap="xs">
          <Group gap="xs">
            <Icon size={20} />
            <Text fw={600} size="sm">{starter.title}</Text>
          </Group>
          <Text size="xs" c="dimmed" lineClamp={2}>
            {starter.prompt}
          </Text>
        </Stack>
      </Card>
    );
  };

  return (
    <Container size="lg" py="xl">
      <Stack gap="xl">
        <div style={{ textAlign: 'center' }}>
          <Title order={2} mb="xs">How can I help you today?</Title>
          <Text c="dimmed">Choose a conversation starter or type your own message</Text>
        </div>
        
        {categories.map(category => {
          const starters = CONVERSATION_STARTERS.filter(s => s.category === category);
          const Icon = getCategoryIcon(category);
          
          return (
            <Stack key={category} gap="md">
              <Group gap="xs">
                <Icon size={20} />
                <Text fw={600}>{category}</Text>
              </Group>
              <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }} spacing="md">
                {starters.map(renderStarter)}
              </SimpleGrid>
            </Stack>
          );
        })}
      </Stack>
    </Container>
  );
}