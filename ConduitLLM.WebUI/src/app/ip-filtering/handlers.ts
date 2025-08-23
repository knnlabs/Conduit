import { useSecurityApi, type IpRule } from '@/hooks/useSecurityApi';
import { withAdminClient } from '@/lib/client/adminClient';
import { notifications } from '@mantine/notifications';

export function useIpFilteringHandlers(
  fetchIpRules: () => Promise<void>,
  setSelectedRules: React.Dispatch<React.SetStateAction<string[]>>
) {
  const { updateIpRule, deleteIpRule, createIpRule } = useSecurityApi();

  const handleBulkOperation = async (operation: string, selectedRules: string[]) => {
    if (selectedRules.length === 0) return;
    
    try {
      // Since bulk operations are not implemented in the Admin SDK, 
      // we'll perform individual operations for each selected rule
      const promises = selectedRules.map(async (ruleId) => {
        const numericId = parseInt(ruleId, 10);
        if (isNaN(numericId)) throw new Error(`Invalid rule ID: ${ruleId}`);

        switch (operation) {
          case 'enable':
            await withAdminClient(client => 
              client.ipFilters.enableFilter(numericId)
            );
            break;
          case 'disable':
            await withAdminClient(client => 
              client.ipFilters.disableFilter(numericId)
            );
            break;
          case 'delete':
            await withAdminClient(client => 
              client.ipFilters.deleteById(numericId)
            );
            break;
          default:
            throw new Error(`Unsupported operation: ${operation}`);
        }
      });

      await Promise.all(promises);

      notifications.show({
        title: 'Success',
        message: `Successfully ${operation}d ${selectedRules.length} rule(s)`,
        color: 'green',
      });

      await fetchIpRules();
      setSelectedRules([]);
    } catch (error) {
      const message = error instanceof Error ? error.message : `Failed to ${operation} rules`;
      notifications.show({
        title: 'Error',
        message,
        color: 'red',
      });
    }
  };

  const handleExport = async (format: string) => {
    try {
      // Get all rules using the Admin SDK
      const filters = await withAdminClient(client => 
        client.ipFilters.list()
      );

      let content: string;
      let mimeType: string;

      if (format === 'json') {
        content = JSON.stringify(filters, null, 2);
        mimeType = 'application/json';
      } else if (format === 'csv') {
        // Convert to CSV format
        const headers = ['id', 'name', 'ipAddressOrCidr', 'filterType', 'isEnabled', 'description', 'createdAt'];
        const csvContent = [
          headers.join(','),
          ...filters.map(filter => 
            headers.map(header => {
              const value = filter[header as keyof typeof filter];
              return typeof value === 'string' ? `"${value}"` : String(value);
            }).join(',')
          )
        ].join('\n');
        content = csvContent;
        mimeType = 'text/csv';
      } else {
        throw new Error(`Unsupported export format: ${format}`);
      }

      // Create and download the file
      const blob = new Blob([content], { type: mimeType });
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
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to export IP rules';
      notifications.show({
        title: 'Error',
        message,
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

      try {
        const content = await file.text();
        let rulesData: Array<{
          name: string;
          ipAddressOrCidr: string;
          filterType: 'whitelist' | 'blacklist';
          isEnabled?: boolean;
          description?: string;
        }>;

        if (format === 'json') {
          const parsed = JSON.parse(content) as unknown;
          if (Array.isArray(parsed)) {
            rulesData = parsed as typeof rulesData;
          } else if (typeof parsed === 'object' && parsed !== null) {
            rulesData = [parsed as typeof rulesData[0]];
          } else {
            throw new Error('Invalid JSON format');
          }
        } else {
          // Parse CSV
          const lines = content.split('\n').filter(line => line.trim());
          const headers = lines[0].split(',').map(h => h.replace(/"/g, '').trim());
          rulesData = lines.slice(1).map(line => {
            const values = line.split(',').map(v => v.replace(/"/g, '').trim());
            const rule: Record<string, string | boolean> = {};
            headers.forEach((header, index) => {
              if (header === 'isEnabled') {
                rule[header] = values[index] === 'true';
              } else {
                rule[header] = values[index];
              }
            });
            return rule as typeof rulesData[0];
          });
        }

        // Import rules using Admin SDK
        let imported = 0;
        let failed = 0;

        for (const ruleData of rulesData) {
          try {
            if (!ruleData.name || !ruleData.ipAddressOrCidr || !ruleData.filterType) {
              failed++;
              continue;
            }

            await withAdminClient(client =>
              client.ipFilters.create({
                name: ruleData.name,
                ipAddressOrCidr: ruleData.ipAddressOrCidr,
                filterType: ruleData.filterType,
                isEnabled: ruleData.isEnabled ?? true,
                description: ruleData.description,
              })
            );
            imported++;
          } catch {
            failed++;
          }
        }

        notifications.show({
          title: 'Success',
          message: `Imported ${imported} rule(s) successfully${failed > 0 ? `, ${failed} failed` : ''}`,
          color: 'green',
        });

        await fetchIpRules();
      } catch (error) {
        const message = error instanceof Error ? error.message : 'Failed to import IP rules';
        notifications.show({
          title: 'Error',
          message,
          color: 'red',
        });
      }
    };

    input.click();
  };

  const handleDeleteRule = async (ruleId: string) => {
    try {
      await deleteIpRule(ruleId);
      await fetchIpRules();
      setSelectedRules(prev => prev.filter(id => id !== ruleId));
    } catch (error) {
      console.error('Failed to delete IP rule:', error);
    }
  };

  const handleToggleRule = async (ruleId: string, enabled: boolean, rules: IpRule[]) => {
    try {
      const rule = rules.find(r => r.id === ruleId);
      if (!rule) return;
      
      await updateIpRule(ruleId, { ...rule, isEnabled: enabled });
      await fetchIpRules();
    } catch (error) {
      console.error('Failed to toggle IP rule:', error);
    }
  };

  const handleModalSubmit = async (
    values: Partial<IpRule>,
    selectedRule: IpRule | null,
    setIsSubmitting: React.Dispatch<React.SetStateAction<boolean>>
  ) => {
    setIsSubmitting(true);
    try {
      if (selectedRule?.id) {
        // Update existing rule
        await updateIpRule(selectedRule.id, values);
      } else {
        // Create new rule
        await createIpRule(values as IpRule);
      }
      await fetchIpRules();
    } catch (error) {
      // Error is already handled by useSecurityApi which shows notifications
      console.error('Failed to save IP rule:', error);
      throw error; // Re-throw so modal doesn't close
    } finally {
      setIsSubmitting(false);
    }
  };

  return {
    handleBulkOperation,
    handleExport,
    handleImport,
    handleDeleteRule,
    handleToggleRule,
    handleModalSubmit,
  };
}