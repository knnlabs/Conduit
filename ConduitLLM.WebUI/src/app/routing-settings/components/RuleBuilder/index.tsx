'use client';

import { useState, useEffect } from 'react';
import {
  Modal,
  Stack,
  Group,
  Button,
  Card,
  Divider,
  Alert,
  Title,
  Text,
} from '@mantine/core';
import { IconInfoCircle, IconTemplate } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { RoutingRule, CreateRoutingRuleRequest } from '../../types/routing';
import { RuleMetadata } from './components/RuleMetadata';
import { ConditionBuilder } from './components/ConditionBuilder';
import { ActionSelector } from './components/ActionSelector';
import { RuleSummary } from './components/RuleSummary';
import { useRuleValidation } from './hooks/useRuleValidation';
import { RULE_TEMPLATES } from './utils/ruleBuilder';

interface RuleBuilderProps {
  isOpen: boolean;
  rule?: RoutingRule | null;
  onClose: () => void;
  onSave: (rule: CreateRoutingRuleRequest) => void;
}

export function RuleBuilder({ isOpen, rule, onClose, onSave }: RuleBuilderProps) {
  const [formData, setFormData] = useState<CreateRoutingRuleRequest>({
    name: '',
    description: '',
    priority: 10,
    conditions: [],
    actions: [],
    enabled: true,
  });

  const [showTemplates, setShowTemplates] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<string | null>(null);

  const { validate, errors, isValid } = useRuleValidation(formData);

  useEffect(() => {
    if (rule) {
      setFormData({
        name: rule.name,
        description: rule.description ?? '',
        priority: rule.priority,
        conditions: rule.conditions.map(c => ({ ...c, logicalOperator: undefined })),
        actions: rule.actions,
        enabled: rule.isEnabled,
      });
    } else {
      setFormData({
        name: '',
        description: '',
        priority: 10,
        conditions: [],
        actions: [],
        enabled: true,
      });
    }
    setSelectedTemplate(null);
    setShowTemplates(false);
  }, [rule, isOpen]);

  const handleSubmit = () => {
    validate();
    
    if (!isValid) {
      notifications.show({
        title: 'Validation Error',
        message: 'Please fix the validation errors before saving',
        color: 'red',
      });
      return;
    }

    onSave(formData);
    onClose();
  };

  const handleTemplateSelect = (templateId: string) => {
    const template = RULE_TEMPLATES.find(t => t.id === templateId);
    if (template) {
      setFormData({
        name: template.name,
        description: template.description,
        priority: template.priority ?? 10,
        conditions: template.conditions,
        actions: template.actions,
        enabled: true,
      });
      setSelectedTemplate(templateId);
      setShowTemplates(false);
      
      notifications.show({
        title: 'Template Applied',
        message: `Applied template: ${template.name}`,
        color: 'green',
      });
    }
  };

  const updateFormData = (updates: Partial<CreateRoutingRuleRequest>) => {
    setFormData(prev => ({ ...prev, ...updates }));
  };

  return (
    <Modal
      opened={isOpen}
      onClose={onClose}
      title={
        <Group>
          {rule ? 'Edit Routing Rule' : 'Create Routing Rule'}
          {selectedTemplate && (
            <Text size="sm" c="dimmed">
              (from template)
            </Text>
          )}
        </Group>
      }
      size="xl"
    >
      <Stack gap="md">
        {/* Template Selection */}
        {!rule && (
          <Card withBorder p="md" bg="gray.0">
            <Group justify="space-between" align="center">
              <div>
                <Title order={6}>Quick Start</Title>
                <Text size="sm" c="dimmed">
                  Use a template to get started quickly
                </Text>
              </div>
              <Button
                leftSection={<IconTemplate size={16} />}
                variant="light"
                size="sm"
                onClick={() => setShowTemplates(!showTemplates)}
              >
                {showTemplates ? 'Hide Templates' : 'Use Template'}
              </Button>
            </Group>
            
            {showTemplates && (
              <Stack gap="xs" mt="md">
                {RULE_TEMPLATES.map((template) => (
                  <Card
                    key={template.id}
                    withBorder
                    p="sm"
                    style={{ cursor: 'pointer' }}
                    onClick={() => handleTemplateSelect(template.id)}
                    bg={selectedTemplate === template.id ? 'blue.0' : 'white'}
                  >
                    <Group justify="space-between">
                      <div>
                        <Text fw={500} size="sm">{template.name}</Text>
                        <Text size="xs" c="dimmed">{template.description}</Text>
                      </div>
                      <Text size="xs" c="blue">
                        Apply
                      </Text>
                    </Group>
                  </Card>
                ))}
              </Stack>
            )}
          </Card>
        )}

        {/* Validation Errors */}
        {errors.length > 0 && (
          <Alert
            icon={<IconInfoCircle size="1rem" />}
            title="Validation Errors"
            color="red"
            variant="light"
          >
            <Stack gap={4}>
              {errors.map((error) => (
                <Text key={`error-${error.slice(0, 50)}`} size="sm">
                  • {error}
                </Text>
              ))}
            </Stack>
          </Alert>
        )}

        {/* Rule Metadata */}
        <RuleMetadata
          formData={formData}
          onUpdate={updateFormData}
          errors={errors}
        />

        {/* Condition Builder */}
        <ConditionBuilder
          conditions={formData.conditions}
          onUpdate={(conditions) => updateFormData({ conditions })}
          errors={errors}
        />

        {/* Action Selector */}
        <ActionSelector
          actions={formData.actions}
          onUpdate={(actions) => updateFormData({ actions })}
          errors={errors}
        />

        {/* Rule Summary */}
        <RuleSummary rule={formData} />

        <Divider />

        {/* Action Buttons */}
        <Group justify="space-between">
          <Group>
            <Text size="sm" c="dimmed">
              Press Ctrl+Enter to save
            </Text>
          </Group>
          <Group>
            <Button variant="subtle" onClick={onClose}>
              Cancel
            </Button>
            <Button 
              onClick={handleSubmit}
              disabled={!isValid}
            >
              {rule ? 'Update Rule' : 'Create Rule'}
            </Button>
          </Group>
        </Group>
      </Stack>
    </Modal>
  );
}