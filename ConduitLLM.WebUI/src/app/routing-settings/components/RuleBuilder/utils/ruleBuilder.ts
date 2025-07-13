export const CONDITION_FIELDS = [
  { 
    value: 'model', 
    label: 'Model', 
    type: 'string',
    description: 'The AI model being requested (e.g., gpt-4, claude-3)',
  },
  { 
    value: 'cost', 
    label: 'Cost per Token', 
    type: 'number',
    description: 'Cost per token in USD',
  },
  { 
    value: 'region', 
    label: 'Region', 
    type: 'string',
    description: 'Geographic region of the request',
  },
  { 
    value: 'virtualKeyId', 
    label: 'Virtual Key', 
    type: 'uuid',
    description: 'Specific virtual key ID',
  },
  { 
    value: 'header', 
    label: 'HTTP Header', 
    type: 'string',
    description: 'Value from HTTP request header',
  },
  { 
    value: 'body', 
    label: 'Request Body', 
    type: 'string',
    description: 'Content from request body',
  },
  { 
    value: 'time', 
    label: 'Time/Date', 
    type: 'datetime',
    description: 'Request timestamp',
  },
  { 
    value: 'load', 
    label: 'System Load', 
    type: 'number',
    description: 'Current system load percentage',
  },
  { 
    value: 'metadata', 
    label: 'Metadata', 
    type: 'string',
    description: 'Custom metadata field',
  },
];

export const OPERATORS = {
  string: [
    { value: 'equals', label: 'Equals' },
    { value: 'not_equals', label: 'Not Equals' },
    { value: 'contains', label: 'Contains' },
    { value: 'not_contains', label: 'Does Not Contain' },
    { value: 'starts_with', label: 'Starts With' },
    { value: 'ends_with', label: 'Ends With' },
    { value: 'regex', label: 'Matches Regex' },
    { value: 'in_list', label: 'In List' },
    { value: 'exists', label: 'Exists' },
  ],
  number: [
    { value: 'equals', label: 'Equals' },
    { value: 'not_equals', label: 'Not Equals' },
    { value: 'greater_than', label: 'Greater Than' },
    { value: 'less_than', label: 'Less Than' },
    { value: 'greater_than_or_equal', label: 'Greater Than or Equal' },
    { value: 'less_than_or_equal', label: 'Less Than or Equal' },
    { value: 'between', label: 'Between' },
    { value: 'in_list', label: 'In List' },
  ],
  uuid: [
    { value: 'equals', label: 'Equals' },
    { value: 'not_equals', label: 'Not Equals' },
    { value: 'in_list', label: 'In List' },
    { value: 'exists', label: 'Exists' },
  ],
  datetime: [
    { value: 'equals', label: 'Equals' },
    { value: 'after', label: 'After' },
    { value: 'before', label: 'Before' },
    { value: 'between', label: 'Between' },
  ],
};

export const ACTION_TYPES = [
  { 
    value: 'route', 
    label: 'Route to Provider',
    description: 'Send the request to a specific provider',
  },
  { 
    value: 'transform', 
    label: 'Transform Request',
    description: 'Modify the request before processing',
  },
  { 
    value: 'cache', 
    label: 'Cache Response',
    description: 'Cache the response for future requests',
  },
  { 
    value: 'rate_limit', 
    label: 'Apply Rate Limit',
    description: 'Limit the rate of requests',
  },
  { 
    value: 'log', 
    label: 'Log Event',
    description: 'Log request details for monitoring',
  },
  { 
    value: 'block', 
    label: 'Block Request',
    description: 'Block the request and return an error',
  },
];

export const getOperatorsForField = (fieldType: string) => {
  const field = CONDITION_FIELDS.find(f => f.value === fieldType);
  if (!field) return OPERATORS.string;
  
  return OPERATORS[field.type as keyof typeof OPERATORS] || OPERATORS.string;
};

export const getValueInputType = (fieldType: string, operator: string) => {
  if (operator === 'exists') return 'none';
  if (operator === 'in_list') return 'multiselect';
  if (operator === 'regex') return 'textarea';
  if (operator === 'between') return 'text'; // Special handling for range inputs
  
  const field = CONDITION_FIELDS.find(f => f.value === fieldType);
  if (!field) return 'text';
  
  switch (field.type) {
    case 'number':
      return 'number';
    case 'datetime':
      return 'datetime';
    case 'uuid':
      return 'text';
    default:
      return 'text';
  }
};

export const getParametersForActionType = (actionType: string) => {
  const parameters = {
    route: [
      {
        name: 'provider_id',
        label: 'Provider',
        type: 'provider',
        required: true,
        description: 'The provider to route the request to',
      },
      {
        name: 'fallback_enabled',
        label: 'Enable Fallback',
        type: 'boolean',
        required: false,
        defaultValue: true,
        description: 'Use fallback providers if primary fails',
      },
      {
        name: 'timeout',
        label: 'Timeout (ms)',
        type: 'number',
        required: false,
        min: 1000,
        max: 300000,
        description: 'Request timeout in milliseconds',
      },
    ],
    transform: [
      {
        name: 'transformation_type',
        label: 'Transformation Type',
        type: 'select',
        required: true,
        options: [
          { value: 'model_mapping', label: 'Model Mapping' },
          { value: 'parameter_injection', label: 'Parameter Injection' },
          { value: 'header_modification', label: 'Header Modification' },
        ],
      },
      {
        name: 'target_model',
        label: 'Target Model',
        type: 'text',
        required: false,
        description: 'Model to transform to (for model mapping)',
      },
    ],
    cache: [
      {
        name: 'cache_duration',
        label: 'Cache Duration (seconds)',
        type: 'number',
        required: true,
        min: 60,
        max: 86400, // 24 hours
        defaultValue: 3600,
        description: 'How long to cache responses',
      },
      {
        name: 'cache_key_strategy',
        label: 'Cache Key Strategy',
        type: 'select',
        required: true,
        options: [
          { value: 'request_hash', label: 'Request Hash' },
          { value: 'model_and_prompt', label: 'Model + Prompt' },
          { value: 'custom', label: 'Custom Key' },
        ],
      },
    ],
    rate_limit: [
      {
        name: 'requests_per_minute',
        label: 'Requests per Minute',
        type: 'number',
        required: true,
        min: 1,
        max: 10000,
        defaultValue: 60,
      },
      {
        name: 'burst_limit',
        label: 'Burst Limit',
        type: 'number',
        required: false,
        min: 1,
        max: 1000,
        description: 'Maximum burst of requests allowed',
      },
    ],
    log: [
      {
        name: 'log_level',
        label: 'Log Level',
        type: 'select',
        required: true,
        options: [
          { value: 'info', label: 'Info' },
          { value: 'warning', label: 'Warning' },
          { value: 'error', label: 'Error' },
          { value: 'debug', label: 'Debug' },
        ],
        defaultValue: 'info',
      },
      {
        name: 'include_request_body',
        label: 'Include Request Body',
        type: 'boolean',
        required: false,
        defaultValue: false,
        description: 'Include request body in log (may contain sensitive data)',
      },
    ],
    block: [
      {
        name: 'block_reason',
        label: 'Block Reason',
        type: 'text',
        required: true,
        placeholder: 'Reason for blocking this request',
      },
      {
        name: 'return_code',
        label: 'HTTP Status Code',
        type: 'select',
        required: true,
        options: [
          { value: '400', label: '400 - Bad Request' },
          { value: '403', label: '403 - Forbidden' },
          { value: '429', label: '429 - Too Many Requests' },
          { value: '503', label: '503 - Service Unavailable' },
        ],
        defaultValue: '403',
      },
    ],
  };

  return parameters[actionType as keyof typeof parameters] || [];
};

export const RULE_TEMPLATES = [
  {
    id: 'premium-models',
    name: 'Premium Models to Primary Provider',
    description: 'Route expensive models to the most reliable provider',
    priority: 10,
    conditions: [
      { 
        type: 'model' as const, 
        operator: 'in_list' as const, 
        value: 'gpt-4, claude-3-opus, claude-3-sonnet' 
      }
    ],
    actions: [
      { 
        type: 'route' as const, 
        target: 'openai-primary',
        parameters: { 
          fallback_enabled: true,
          timeout: 30000 
        } 
      }
    ]
  },
  {
    id: 'cost-optimization',
    name: 'Cost-Optimized Routing',
    description: 'Route based on cost thresholds to minimize expenses',
    priority: 20,
    conditions: [
      { 
        type: 'cost' as const, 
        operator: 'less_than' as const, 
        value: '0.02' 
      }
    ],
    actions: [
      { 
        type: 'route' as const, 
        target: 'anthropic-primary',
        parameters: { 
          fallback_enabled: true 
        } 
      }
    ]
  },
  {
    id: 'high-load-fallback',
    name: 'High Load Fallback',
    description: 'Use backup providers when system load is high',
    priority: 30,
    conditions: [
      { 
        type: 'load' as const, 
        operator: 'greater_than' as const, 
        value: '80' 
      }
    ],
    actions: [
      { 
        type: 'route' as const, 
        target: 'local-llama',
        parameters: { 
          timeout: 60000 
        } 
      }
    ]
  },
  {
    id: 'development-caching',
    name: 'Development Request Caching',
    description: 'Cache responses for development and testing environments',
    priority: 40,
    conditions: [
      { 
        type: 'header' as const, 
        field: 'X-Environment',
        operator: 'equals' as const, 
        value: 'development' 
      }
    ],
    actions: [
      { 
        type: 'cache' as const, 
        parameters: { 
          cache_duration: 3600,
          cache_key_strategy: 'request_hash' 
        } 
      }
    ]
  },
  {
    id: 'rate-limit-user',
    name: 'User Rate Limiting',
    description: 'Apply rate limits to prevent abuse from specific users',
    priority: 5,
    conditions: [
      { 
        type: 'virtualKeyId' as const, 
        operator: 'in_list' as const, 
        value: 'key-suspicious-1, key-suspicious-2' 
      }
    ],
    actions: [
      { 
        type: 'rate_limit' as const, 
        parameters: { 
          requests_per_minute: 10,
          burst_limit: 5 
        } 
      }
    ]
  },
  {
    id: 'security-block',
    name: 'Security Block',
    description: 'Block suspicious requests that may be malicious',
    priority: 1,
    conditions: [
      { 
        type: 'body' as const, 
        operator: 'contains' as const, 
        value: 'jailbreak' 
      }
    ],
    actions: [
      { 
        type: 'block' as const, 
        parameters: { 
          block_reason: 'Suspicious content detected',
          return_code: '403' 
        } 
      }
    ]
  }
];