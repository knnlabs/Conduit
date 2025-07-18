'use client';

import { useState, useEffect, useCallback } from 'react';
import {
  Card,
  Stack,
  Group,
  Button,
  Text,
  Badge,
  Alert,
  Center,
  Loader,
  Title,
} from '@mantine/core';
import {
  IconPlus,
  IconRefresh,
  IconAlertCircle,
  IconRoute,
} from '@tabler/icons-react';
import { RulesList } from './RulesList';
import { RuleBuilder } from '../RuleBuilder';
import { useRoutingRules } from '../../hooks/useRoutingRules';
import { RoutingRule, CreateRoutingRuleRequest } from '../../types/routing';

interface RulesTabProps {
  onLoadingChange: (loading: boolean) => void;
}

export function RulesTab({ onLoadingChange }: RulesTabProps) {
  const [rules, setRules] = useState<RoutingRule[]>([]);
  const [selectedRule, setSelectedRule] = useState<RoutingRule | null>(null);
  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  
  const {
    getRules,
    createRule,
    updateRule,
    deleteRule,
    toggleRule,
    isLoading,
    error,
  } = useRoutingRules();

  useEffect(() => {
    onLoadingChange(isLoading);
  }, [isLoading, onLoadingChange]);

  const loadRules = useCallback(async () => {
    try {
      const data = await getRules();
      setRules(data);
    } catch {
      // Error is handled by the hook
    }
  }, [getRules]);

  useEffect(() => {
    void loadRules();
  }, [refreshKey, loadRules]);

  const handleRefresh = () => {
    setRefreshKey(prev => prev + 1);
  };

  const handleCreateRule = () => {
    setSelectedRule(null);
    setIsEditorOpen(true);
  };

  const handleEditRule = (rule: RoutingRule) => {
    setSelectedRule(rule);
    setIsEditorOpen(true);
  };

  const handleSaveRule = async (ruleData: CreateRoutingRuleRequest) => {
    try {
      if (selectedRule) {
        await updateRule(selectedRule.id, ruleData);
      } else {
        await createRule(ruleData);
      }
      setIsEditorOpen(false);
      handleRefresh();
    } catch {
      // Error is handled by the hook
    }
  };

  const handleDeleteRule = async (id: string) => {
    try {
      await deleteRule(id);
      handleRefresh();
    } catch {
      // Error is handled by the hook
    }
  };

  const handleToggleRule = async (id: string, enabled: boolean) => {
    try {
      await toggleRule(id, enabled);
      handleRefresh();
    } catch {
      // Error is handled by the hook
    }
  };

  if (error) {
    return (
      <Alert icon={<IconAlertCircle size="1rem" />} title="Error" color="red">
        {error}
      </Alert>
    );
  }

  return (
    <Stack gap="md">
      {/* Header */}
      <Card shadow="sm" p="md" radius="md" withBorder>
        <Group justify="space-between" align="flex-start">
          <div>
            <Title order={4}>Routing Rules</Title>
            <Text c="dimmed" size="sm" mt={4}>
              Create and manage rules to control how requests are routed to different providers
            </Text>
            <Group mt="xs" gap="xs">
              <Badge variant="light" color="blue">
                {rules.length} {rules.length === 1 ? 'rule' : 'rules'}
              </Badge>
              <Badge variant="light" color="green">
                {rules.filter(r => r.isEnabled).length} active
              </Badge>
            </Group>
          </div>
          <Group>
            <Button
              leftSection={<IconRefresh size={16} />}
              variant="subtle"
              onClick={handleRefresh}
              loading={isLoading}
            >
              Refresh
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={handleCreateRule}
            >
              Add Rule
            </Button>
          </Group>
        </Group>
      </Card>

      {/* Rules List */}
      {(() => {
        if (isLoading && rules.length === 0) {
          return (
            <Center h={200}>
              <Loader />
            </Center>
          );
        }
        
        if (rules.length === 0) {
          return (
            <Card shadow="sm" p="xl" radius="md" withBorder>
              <Center h={200}>
                <Stack align="center" gap="md">
                  <IconRoute size={48} stroke={1.5} color="gray" />
                  <div style={{ textAlign: 'center' }}>
                    <Text size="lg" fw={500}>No routing rules found</Text>
                    <Text c="dimmed" size="sm" mt={4}>
                      Create your first routing rule to control how requests are handled
                    </Text>
                  </div>
                  <Button
                    leftSection={<IconPlus size={16} />}
                    onClick={handleCreateRule}
                  >
                    Create First Rule
                  </Button>
                </Stack>
              </Center>
            </Card>
          );
        }
        
        return (
          <RulesList
            rules={rules}
            onEdit={handleEditRule}
            onDelete={(id: string) => void handleDeleteRule(id)}
            onToggle={(id: string, enabled: boolean) => void handleToggleRule(id, enabled)}
          />
        );
      })()}

      {/* Rule Builder Modal */}
      <RuleBuilder
        isOpen={isEditorOpen}
        rule={selectedRule}
        onClose={() => setIsEditorOpen(false)}
        onSave={(ruleData) => void handleSaveRule(ruleData)}
      />
    </Stack>
  );
}