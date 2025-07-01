'use client';

import { LineChart, BarChart, PieChart } from '@mantine/charts';
import { Card, Group, Text, Select, Button, Stack } from '@mantine/core';
import { IconDownload, IconRefresh } from '@tabler/icons-react';
import { useState } from 'react';

interface CostChartProps {
  data: any[];
  title: string;
  type: 'line' | 'bar' | 'pie';
  valueKey: string;
  nameKey: string;
  timeKey?: string;
  height?: number;
  showControls?: boolean;
  onRefresh?: () => void;
  onExport?: () => void;
}

export function CostChart({
  data,
  title,
  type,
  valueKey,
  nameKey,
  timeKey,
  height = 300,
  showControls = true,
  onRefresh,
  onExport,
}: CostChartProps) {
  const [timeRange, setTimeRange] = useState('7d');

  const timeRangeOptions = [
    { value: '24h', label: 'Last 24 Hours' },
    { value: '7d', label: 'Last 7 Days' },
    { value: '30d', label: 'Last 30 Days' },
    { value: '90d', label: 'Last 3 Months' },
  ];

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2,
      maximumFractionDigits: 4,
    }).format(value);
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: timeRange === '24h' ? '2-digit' : undefined,
    });
  };

  const renderChart = () => {
    switch (type) {
      case 'line':
        return (
          <LineChart
            h={height}
            data={data}
            dataKey={timeKey || nameKey}
            series={[{ name: valueKey, color: 'blue.6' }]}
            valueFormatter={formatCurrency}
            xAxisProps={{
              tickFormatter: timeKey ? formatDate : undefined,
            }}
            curveType="monotone"
            strokeWidth={2}
            dotProps={{ strokeWidth: 2, r: 4 }}
            activeDotProps={{ strokeWidth: 2, r: 6 }}
          />
        );
      
      case 'bar':
        return (
          <BarChart
            h={height}
            data={data}
            dataKey={nameKey}
            series={[{ name: valueKey, color: 'blue.6' }]}
            valueFormatter={formatCurrency}
            barProps={{ radius: 4 }}
          />
        );
      
      case 'pie':
        return (
          <PieChart
            h={height}
            data={data.map(item => ({
              name: item[nameKey],
              value: item[valueKey],
              color: `blue.${Math.floor(Math.random() * 9) + 1}`,
            }))}
            valueFormatter={formatCurrency}
            withLabelsLine={false}
            labelsPosition="outside"
            labelsType="percent"
            withTooltip
            tooltipDataSource="segment"
          />
        );
      
      default:
        return null;
    }
  };

  return (
    <Card withBorder>
      <Stack gap="md">
        <Group justify="space-between">
          <Text fw={600}>{title}</Text>
          
          {showControls && (
            <Group gap="xs">
              {timeKey && (
                <Select
                  value={timeRange}
                  onChange={(value) => setTimeRange(value || '7d')}
                  data={timeRangeOptions}
                  size="xs"
                  w={150}
                />
              )}
              
              {onRefresh && (
                <Button
                  variant="subtle"
                  size="xs"
                  leftSection={<IconRefresh size={14} />}
                  onClick={onRefresh}
                >
                  Refresh
                </Button>
              )}
              
              {onExport && (
                <Button
                  variant="subtle"
                  size="xs"
                  leftSection={<IconDownload size={14} />}
                  onClick={onExport}
                >
                  Export
                </Button>
              )}
            </Group>
          )}
        </Group>

        {data.length > 0 ? (
          renderChart()
        ) : (
          <div
            style={{
              height,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: 'var(--mantine-color-dimmed)',
            }}
          >
            <Text size="sm">No data available</Text>
          </div>
        )}
      </Stack>
    </Card>
  );
}