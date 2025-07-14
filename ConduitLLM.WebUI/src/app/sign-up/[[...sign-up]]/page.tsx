import { SignUp } from '@clerk/nextjs';
import { Center, Container } from '@mantine/core';

export default function SignUpPage() {
  return (
    <Container size={420} style={{ minHeight: '100vh', display: 'flex', alignItems: 'center' }}>
      <Center w="100%">
        <SignUp />
      </Center>
    </Container>
  );
}