'use client';

import { useState } from 'react';
import { Container, Title, Text, Tabs, Stack, LoadingOverlay, Tooltip } from '@mantine/core';
import { 
  IconRoute, 
  IconServer, 
  IconTestPipe
} from '@tabler/icons-react';
import { RulesTab } from './components/RulesTab';
import { ProvidersTab } from './components/ProvidersTab';
import { TestingTab } from './components/TestingTab';

export default function RoutingSettingsPage() {
  const [activeTab, setActiveTab] = useState<string | null>('rules');
  const [isLoading, setIsLoading] = useState(false);

  return (
    <Container size="xl">
      <Stack gap="md">
        <div>
          <Title order={2}>Routing Settings</Title>
          <Text c="dimmed" size="sm" mt={4}>
            Configure request routing rules, provider priorities, and test routing logic
          </Text>
        </div>


        <div style={{ position: 'relative', minHeight: 600 }}>
          <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
          
          <Tabs value={activeTab} onChange={setActiveTab}>
            <Tabs.List>
              <Tooltip label="Create and manage routing rules based on request conditions">
                <Tabs.Tab 
                  value="rules" 
                  leftSection={<IconRoute size={16} />}
                >
                  Routing Rules
                </Tabs.Tab>
              </Tooltip>
              <Tooltip label="Configure provider priority order and fallback chains">
                <Tabs.Tab 
                  value="providers" 
                  leftSection={<IconServer size={16} />}
                >
                  Provider Priority
                </Tabs.Tab>
              </Tooltip>
              <Tooltip label="Test routing rules and validate routing behavior with sample requests">
                <Tabs.Tab 
                  value="testing" 
                  leftSection={<IconTestPipe size={16} />}
                >
                  Testing & Validation
                </Tabs.Tab>
              </Tooltip>
            </Tabs.List>

            <Tabs.Panel value="rules" pt="md">
              <RulesTab onLoadingChange={setIsLoading} />
            </Tabs.Panel>

            <Tabs.Panel value="providers" pt="md">
              <ProvidersTab onLoadingChange={setIsLoading} />
            </Tabs.Panel>

            <Tabs.Panel value="testing" pt="md">
              <TestingTab onLoadingChange={setIsLoading} />
            </Tabs.Panel>
          </Tabs>
        </div>
      </Stack>
    </Container>
  );
}