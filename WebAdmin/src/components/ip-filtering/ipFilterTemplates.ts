export interface IpTemplateRule {
  ipAddressOrCidr: string;
  name: string;
  description: string;
}

export interface IpFilterTemplate {
  id: string;
  label: string;
  description: string;
  rules: IpTemplateRule[];
}

export const ipFilterTemplates: IpFilterTemplate[] = [
  {
    id: 'all-private',
    label: 'Allow All Private Networks (RFC 1918)',
    description: 'Allows all private/internal network ranges plus localhost',
    rules: [
      {
        ipAddressOrCidr: '10.0.0.0/8',
        name: 'Private Network (Class A)',
        description: 'RFC 1918 - 10.0.0.0 to 10.255.255.255',
      },
      {
        ipAddressOrCidr: '172.16.0.0/12',
        name: 'Private Network (Class B)',
        description: 'RFC 1918 - 172.16.0.0 to 172.31.255.255',
      },
      {
        ipAddressOrCidr: '192.168.0.0/16',
        name: 'Private Network (Class C)',
        description: 'RFC 1918 - 192.168.0.0 to 192.168.255.255',
      },
      {
        ipAddressOrCidr: '127.0.0.0/8',
        name: 'Localhost',
        description: 'Loopback addresses - 127.0.0.0 to 127.255.255.255',
      },
    ],
  },
  {
    id: 'home-network',
    label: 'Allow Home Network (192.168.x.x)',
    description: 'Common home router network range',
    rules: [
      {
        ipAddressOrCidr: '192.168.0.0/16',
        name: 'Home Network',
        description: 'RFC 1918 - 192.168.0.0 to 192.168.255.255',
      },
    ],
  },
  {
    id: 'corporate-network',
    label: 'Allow Corporate Network (10.x.x.x)',
    description: 'Large corporate/enterprise network range',
    rules: [
      {
        ipAddressOrCidr: '10.0.0.0/8',
        name: 'Corporate Network',
        description: 'RFC 1918 - 10.0.0.0 to 10.255.255.255',
      },
    ],
  },
  {
    id: 'docker-network',
    label: 'Allow Docker/Container Network (172.16-31.x.x)',
    description: 'Docker and container default network range',
    rules: [
      {
        ipAddressOrCidr: '172.16.0.0/12',
        name: 'Docker/Container Network',
        description: 'RFC 1918 - 172.16.0.0 to 172.31.255.255',
      },
    ],
  },
  {
    id: 'localhost-only',
    label: 'Allow Localhost Only',
    description: 'Only allow connections from the local machine',
    rules: [
      {
        ipAddressOrCidr: '127.0.0.0/8',
        name: 'Localhost',
        description: 'Loopback addresses - 127.0.0.0 to 127.255.255.255',
      },
    ],
  },
];
