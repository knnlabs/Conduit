'use client';

import { Group, Text, Anchor, Container, Divider } from '@mantine/core';
import { IconBrandGithub, IconLicense, IconBook } from '@tabler/icons-react';

export function Footer() {
  return (
    <footer style={{ marginTop: 'auto' }}>
      <Divider />
      <Container py="md">
        <Group justify="space-between" align="center">
          <Text size="sm" c="dimmed">
            © 2024 Conduit AI Gateway. All rights reserved.
          </Text>
          
          <Group gap="md">
            <Anchor
              href="https://github.com/knnlabs/Conduit"
              target="_blank"
              size="sm"
              c="dimmed"
              style={{ display: 'flex', alignItems: 'center', gap: 4 }}
            >
              <IconBrandGithub size={16} />
              GitHub
            </Anchor>
            
            <Anchor
              href="https://github.com/knnlabs/Conduit/issues"
              target="_blank"
              size="sm"
              c="dimmed"
              style={{ display: 'flex', alignItems: 'center', gap: 4 }}
            >
              <IconBook size={16} />
              Documentation
            </Anchor>
            
            <Anchor
              href="#"
              size="sm"
              c="dimmed"
              style={{ display: 'flex', alignItems: 'center', gap: 4 }}
            >
              <IconLicense size={16} />
              License
            </Anchor>
          </Group>
          
          <Text size="xs" c="dimmed">
            Version 2.0.0
          </Text>
        </Group>
      </Container>
    </footer>
  );
}