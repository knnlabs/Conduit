'use client';

import {
  Stack,
  Title,
  Text,
  Card,
  Group,
  Button,
  SimpleGrid,
  ThemeIcon,
  LoadingOverlay,
  Alert,
  Tabs,
  Badge,
  Menu,
  rem,
} from '@mantine/core';
import {
  IconShield,
  IconPlus,
  IconDownload,
  IconUpload,
  IconFilter,
  IconShieldCheck,
  IconShieldX,
  IconClock,
  IconAlertCircle,
  IconFileTypeCsv,
  IconJson,
  IconTestPipe,
} from '@tabler/icons-react';
import { useState, useEffect } from 'react';
import { useDisclosure } from '@mantine/hooks';
import { useSecurityApi, type IpRule, type IpStats } from '@/hooks/useSecurityApi';
import { notifications } from '@mantine/notifications';
import { IpRulesTable } from '@/components/ip-filtering/IpRulesTable';
import { IpRuleModal } from '@/components/ip-filtering/IpRuleModal';
import { IpTestModal } from '@/components/ip-filtering/IpTestModal';


export default function IpFilteringPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [rules, setRules] = useState<IpRule[]>([]);
  const [stats, setStats] = useState<IpStats | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>('all');
  const [selectedRules, setSelectedRules] = useState<string[]>([]);
  const [selectedRule, setSelectedRule] = useState<IpRule | null>(null);
  const [modalOpened, { open: openModal, close: closeModal }] = useDisclosure(false);
  const [testModalOpened, { open: openTestModal, close: closeTestModal }] = useDisclosure(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const { getIpRules, createIpRule, updateIpRule, deleteIpRule, getIpStats, error } = useSecurityApi();

  useEffect(() => {
    fetchIpRules();
  }, []);

  const fetchIpRules = async () => {
    try {
      setIsLoading(true);
      const fetchedRules = await getIpRules();
      setRules(fetchedRules);
      
      // Calculate statistics from the rules data
      const calculatedStats: IpStats = {
        totalRules: fetchedRules.length,
        allowRules: fetchedRules.filter(r => r.action === 'allow').length,
        blockRules: fetchedRules.filter(r => r.action === 'block').length,
        activeRules: fetchedRules.filter(r => r.isEnabled !== false).length,
        blockedRequests24h: 0, // This would need to come from a real endpoint
        lastRuleUpdate: fetchedRules.length > 0 
          ? new Date(Math.max(...fetchedRules.map(r => new Date(r.createdAt || '').getTime()).filter(t => !isNaN(t)))).toISOString()
          : null,
      };
      setStats(calculatedStats);
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to load IP rules',
        color: 'red',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = () => {
    fetchIpRules();
  };

  const handleCreateRule = () => {
    setSelectedRule(null);
    openModal();
  };

  const handleBulkOperation = async (operation: string) => {
    if (selectedRules.length === 0) return;
    
    try {
      const response = await fetch('/api/admin/security/ip-rules/bulk', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          operation,
          ruleIds: selectedRules,
        }),
      });

      const result = await response.json();

      if (!response.ok) {
        throw new Error(result.error || `Failed to ${operation} rules`);
      }

      notifications.show({
        title: 'Success',
        message: `Successfully ${operation}d ${selectedRules.length} rule(s)`,
        color: 'green',
      });

      await fetchIpRules();
      setSelectedRules([]);
    } catch (err) {
      const message = err instanceof Error ? err.message : `Failed to ${operation} rules`;
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
    }
  };

  const handleExport = async (format: string) => {
    try {
      const response = await fetch(`/api/admin/security/ip-rules/export?format=${format}`);
      
      if (!response.ok) {
        throw new Error('Failed to export IP rules');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `ip-rules.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);

      notifications.show({
        title: 'Success',
        message: `IP rules exported as ${format.toUpperCase()}`,
        color: 'green',
      });
    } catch (err) {
      notifications.show({
        title: 'Error',
        message: 'Failed to export IP rules',
        color: 'red',
      });
    }
  };

  const handleImport = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json,.csv';
    
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (!file) return;

      const format = file.name.endsWith('.csv') ? 'csv' : 'json';
      const formData = new FormData();
      formData.append('file', file);
      formData.append('format', format);

      try {
        const response = await fetch('/api/admin/security/ip-rules/import', {
          method: 'POST',
          body: formData,
        });

        const result = await response.json();

        if (!response.ok) {
          throw new Error(result.error || 'Failed to import IP rules');
        }

        notifications.show({
          title: 'Success',
          message: `Imported ${result.imported} rule(s) successfully${result.failed ? `, ${result.failed} failed` : ''}`,
          color: 'green',
        });

        await fetchIpRules();
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Failed to import IP rules';
        notifications.show({
          title: 'Error',
          message,
          color: 'red',
        });
      }
    };

    input.click();
  };

  const handleTestIp = () => {
    openTestModal();
  };

  const handleEditRule = (rule: IpRule) => {
    setSelectedRule(rule);
    openModal();
  };

  const handleDeleteRule = async (ruleId: string) => {
    try {
      await deleteIpRule(ruleId);
      await fetchIpRules();
      setSelectedRules(prev => prev.filter(id => id !== ruleId));
    } catch (err) {
      console.error('Failed to delete IP rule:', err);
    }
  };

  const handleToggleRule = async (ruleId: string, enabled: boolean) => {
    try {
      const rule = rules.find(r => r.id === ruleId);
      if (!rule) return;
      
      await updateIpRule(ruleId, { ...rule, isEnabled: enabled });
      await fetchIpRules();
    } catch (err) {
      console.error('Failed to toggle IP rule:', err);
    }
  };

  const handleModalSubmit = async (values: Partial<IpRule>) => {
    setIsSubmitting(true);
    try {
      if (selectedRule?.id) {
        // Update existing rule
        await updateIpRule(selectedRule.id, values);
        notifications.show({
          title: 'Success',
          message: 'IP rule updated successfully',
          color: 'green',
        });
      } else {
        // Create new rule
        await createIpRule(values as IpRule);
        notifications.show({
          title: 'Success',
          message: 'IP rule created successfully',
          color: 'green',
        });
      }
      await fetchIpRules();
      closeModal();
    } catch (err) {
      // Error is already handled by useSecurityApi
      console.error('Failed to save IP rule:', err);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Filter rules based on active tab
  const filteredRules = rules.filter(rule => {
    if (activeTab === 'allow') return rule.action === 'allow';
    if (activeTab === 'block') return rule.action === 'block';
    return true;
  });

  const statCards = [
    {
      title: 'Total Rules',
      value: stats?.totalRules || 0,
      description: 'Active IP filtering rules',
      icon: IconShield,
      color: 'blue',
    },
    {
      title: 'Allow Rules',
      value: stats?.allowRules || 0,
      description: 'Whitelisted IPs',
      icon: IconShieldCheck,
      color: 'green',
    },
    {
      title: 'Block Rules',
      value: stats?.blockRules || 0,
      description: 'Blacklisted IPs',
      icon: IconShieldX,
      color: 'red',
    },
    {
      title: 'Blocked Today',
      value: stats?.blockedRequests24h || 0,
      description: 'Requests blocked in 24h',
      icon: IconClock,
      color: 'orange',
    },
  ];

  if (error) {
    return (
      <Stack gap="xl">
        <div>
          <Title order={1}>IP Filtering</Title>
          <Text c="dimmed">Manage IP access control and filtering rules</Text>
        </div>
        
        <Alert 
          icon={<IconAlertCircle size={16} />} 
          title="Error loading IP rules"
          color="red"
        >
          {error}
        </Alert>
      </Stack>
    );
  }

  return (
    <>
      <Stack gap="xl">
      {/* Header */}
      <Card shadow="sm" p="md" radius="md">
        <Group justify="space-between" align="center">
          <div>
            <Title order={2}>IP Filtering</Title>
            <Text size="sm" c="dimmed" mt={4}>
              Manage IP access control and filtering rules
            </Text>
          </div>
          
          <Group gap="sm">
            <Button
              variant="light"
              leftSection={<IconTestPipe size={16} />}
              onClick={handleTestIp}
            >
              Test IP
            </Button>
            
            <Menu shadow="md" width={200}>
              <Menu.Target>
                <Button 
                  variant="light" 
                  leftSection={<IconDownload size={16} />}
                >
                  Export
                </Button>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Item
                  leftSection={<IconJson style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => handleExport('json')}
                >
                  Export as JSON
                </Menu.Item>
                <Menu.Item
                  leftSection={<IconFileTypeCsv style={{ width: rem(14), height: rem(14) }} />}
                  onClick={() => handleExport('csv')}
                >
                  Export as CSV
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
            
            <Button
              variant="light"
              leftSection={<IconUpload size={16} />}
              onClick={handleImport}
            >
              Import
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

      {/* Statistics Cards */}
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} spacing="md">
        {statCards.map((stat) => (
          <Card key={stat.title} padding="lg" radius="md" withBorder>
            <Stack gap="md">
              <Group justify="space-between" align="flex-start">
                <Stack gap={4} style={{ flex: 1 }}>
                  <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                    {stat.title}
                  </Text>
                  <Text fw={700} size="xl" lh={1}>
                    {stat.value.toLocaleString()}
                  </Text>
                </Stack>
                <ThemeIcon color={stat.color} variant="light" size={40} radius="md">
                  <stat.icon size={20} />
                </ThemeIcon>
              </Group>
              <Text size="xs" c="dimmed" lh={1.2}>
                {stat.description}
              </Text>
            </Stack>
          </Card>
        ))}
      </SimpleGrid>

      {/* Rules Table Container */}
      <Card shadow="sm" p={0} radius="md" withBorder>
        <Tabs value={activeTab} onChange={setActiveTab}>
          <Tabs.List>
            <Tabs.Tab 
              value="all" 
              leftSection={<IconFilter size={16} />}
              rightSection={
                <Badge size="sm" variant="filled" color="gray">
                  {stats?.totalRules || 0}
                </Badge>
              }
            >
              All Rules
            </Tabs.Tab>
            <Tabs.Tab 
              value="allow" 
              leftSection={<IconShieldCheck size={16} />}
              rightSection={
                <Badge size="sm" variant="filled" color="green">
                  {stats?.allowRules || 0}
                </Badge>
              }
            >
              Allow List
            </Tabs.Tab>
            <Tabs.Tab 
              value="block" 
              leftSection={<IconShieldX size={16} />}
              rightSection={
                <Badge size="sm" variant="filled" color="red">
                  {stats?.blockRules || 0}
                </Badge>
              }
            >
              Block List
            </Tabs.Tab>
          </Tabs.List>

          <div style={{ position: 'relative', minHeight: 400 }}>
            <LoadingOverlay visible={isLoading} overlayProps={{ radius: 'sm', blur: 2 }} />
            
            {/* Bulk Actions Bar */}
            {selectedRules.length > 0 && (
              <Card.Section p="md" withBorder>
                <Group justify="space-between">
                  <Text size="sm">
                    {selectedRules.length} rule{selectedRules.length !== 1 ? 's' : ''} selected
                  </Text>
                  <Group gap="xs">
                    <Button 
                      size="xs" 
                      variant="light"
                      onClick={() => handleBulkOperation('enable')}
                    >
                      Enable
                    </Button>
                    <Button 
                      size="xs" 
                      variant="light"
                      onClick={() => handleBulkOperation('disable')}
                    >
                      Disable
                    </Button>
                    <Button 
                      size="xs" 
                      variant="light" 
                      color="red"
                      onClick={() => handleBulkOperation('delete')}
                    >
                      Delete
                    </Button>
                  </Group>
                </Group>
              </Card.Section>
            )}
            
            {/* IP Rules Table */}
            <Card.Section>
              <IpRulesTable 
                data={filteredRules}
                selectedRules={selectedRules}
                onSelectionChange={setSelectedRules}
                onEdit={handleEditRule}
                onDelete={handleDeleteRule}
                onToggle={handleToggleRule}
              />
            </Card.Section>
          </div>
        </Tabs>
      </Card>
    </Stack>

    {/* IP Rule Modal */}
    <IpRuleModal
      opened={modalOpened}
      onClose={closeModal}
      onSubmit={handleModalSubmit}
      rule={selectedRule}
      isLoading={isSubmitting}
    />

    {/* IP Test Modal */}
    <IpTestModal
      opened={testModalOpened}
      onClose={closeTestModal}
    />
    </>
  );
}