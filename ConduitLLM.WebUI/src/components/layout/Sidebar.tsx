'use client';

import { useState } from 'react';
import {
  ScrollArea,
  NavLink,
  Text,
  Divider,
  Stack,
  Badge,
  Group,
  ThemeIcon,
  Collapse,
  UnstyledButton,
  Box,
} from '@mantine/core';
import { IconChevronRight } from '@tabler/icons-react';
import { useRouter, usePathname } from 'next/navigation';
import { useNavigationStore } from '@/stores/useNavigationStore';
import { NavigationSection, NavigationItem } from '@/types/navigation';

export function Sidebar() {
  const router = useRouter();
  const pathname = usePathname();
  const { sections, activeItem, setActiveItem, toggleSection: _toggleSection } = useNavigationStore();
  const [openSections, setOpenSections] = useState<Set<string>>(
    new Set(sections.map(s => s.id))
  );

  const handleItemClick = (item: NavigationItem) => {
    if (item.disabled) return;
    
    setActiveItem(item.id);
    router.push(item.href);
  };

  const handleSectionToggle = (sectionId: string) => {
    setOpenSections(prev => {
      const newSet = new Set(prev);
      if (newSet.has(sectionId)) {
        newSet.delete(sectionId);
      } else {
        newSet.add(sectionId);
      }
      return newSet;
    });
  };

  const isItemActive = (item: NavigationItem) => {
    return pathname === item.href || activeItem === item.id;
  };

  const renderNavigationItem = (item: NavigationItem) => {
    const active = isItemActive(item);
    
    return (
      <NavLink
        key={item.id}
        label={item.label}
        leftSection={
          <ThemeIcon 
            size="sm" 
            variant={active ? 'filled' : 'light'} 
            color={item.color || 'blue'}
          >
            <item.icon size={16} />
          </ThemeIcon>
        }
        rightSection={
          item.badge && (
            <Badge size="xs" variant="light" color={item.color || 'blue'}>
              {item.badge}
            </Badge>
          )
        }
        active={active}
        disabled={item.disabled}
        onClick={() => handleItemClick(item)}
        style={{
          opacity: item.disabled ? 0.5 : 1,
          cursor: item.disabled ? 'not-allowed' : 'pointer',
        }}
      />
    );
  };

  const renderSection = (section: NavigationSection) => {
    const isOpen = openSections.has(section.id);
    
    return (
      <Box key={section.id}>
        <UnstyledButton
          onClick={() => handleSectionToggle(section.id)}
          w="100%"
          p="xs"
          style={{ borderRadius: 4 }}
        >
          <Group justify="space-between" wrap="nowrap">
            <Text size="sm" fw={600} c="dimmed" tt="uppercase" lts={0.5}>
              {section.label}
            </Text>
            <IconChevronRight
              size={14}
              style={{
                transform: isOpen ? 'rotate(90deg)' : 'none',
                transition: 'transform 200ms ease',
              }}
            />
          </Group>
        </UnstyledButton>

        <Collapse in={isOpen}>
          <Stack gap={2} mt="xs">
            {section.items.map(renderNavigationItem)}
          </Stack>
        </Collapse>
      </Box>
    );
  };

  return (
    <ScrollArea h="100%" p="md">
      <Stack gap="lg">
        {sections.map((section, index) => (
          <div key={section.id}>
            {renderSection(section)}
            {index < sections.length - 1 && <Divider />}
          </div>
        ))}
      </Stack>
    </ScrollArea>
  );
}