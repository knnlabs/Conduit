'use client';

import { useState } from 'react';
import { Container, Title, Text, Tabs, Stack, LoadingOverlay, Alert } from '@mantine/core';
import { 
  IconRoute, 
  IconServer, 
  IconTestPipe,
  IconSettings,
  IconInfoCircle
} from '@tabler/icons-react';
import { ProtectedRoute } from '@/components/auth/ProtectedRoute';
import { RulesTab } from './components/RulesTab';
import { ProvidersTab } from './components/ProvidersTab';
import { TestingTab } from './components/TestingTab';

export default function RoutingSettingsPage() {
  const [activeTab, setActiveTab] = useState<string | null>('rules');
  const [isLoading, setIsLoading] = useState(false);

  return (
    <ProtectedRoute>
      <Container size="xl">
        <Stack gap="md">
          <div>
            <Title order={2}>Routing Settings</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Configure request routing rules, provider priorities, and test routing logic
            </Text>
          </div>

          <Alert 
            icon={<IconInfoCircle size="1rem" />} 
            title="Development Preview" 
            color="blue"
            variant="light"
          >
            This interface is currently using mock data for demonstration purposes. 
            The backend API endpoints for routing configuration are not yet implemented.
          </Alert>

          <div style={{ position: 'relative', minHeight: 600 }}>
            <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
            
            <Tabs value={activeTab} onChange={setActiveTab}>
              <Tabs.List>
                <Tabs.Tab 
                  value="rules" 
                  leftSection={<IconRoute size={16} />}
                >
                  Routing Rules
                </Tabs.Tab>
                <Tabs.Tab 
                  value="providers" 
                  leftSection={<IconServer size={16} />}
                >
                  Provider Priority
                </Tabs.Tab>
                <Tabs.Tab 
                  value="testing" 
                  leftSection={<IconTestPipe size={16} />}
                >
                  Testing & Validation
                </Tabs.Tab>
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
    </ProtectedRoute>
  );
}