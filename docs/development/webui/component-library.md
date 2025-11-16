# Conduit Component Library

A comprehensive design system and component library for building consistent, accessible, and maintainable user interfaces in the Conduit ecosystem.

## Overview

The Conduit Component Library provides a complete set of reusable UI components built with modern web standards, accessibility best practices, and design tokens for consistent theming across all applications.

## Documentation Structure

The component library documentation has been organized by component categories and usage patterns:

### 🧩 Core Components
- **[Form Components](./components/forms.md)** - Inputs, buttons, validation, and form patterns
- **[Layout Components](./components/layout.md)** - Grids, containers, spacing, and responsive layouts
- **[Navigation Components](./components/navigation.md)** - Menus, breadcrumbs, tabs, and navigation patterns

### 🎨 UI Elements
- **[Data Display](./components/data-display.md)** - Tables, cards, lists, and data visualization
- **[Feedback Components](./components/feedback.md)** - Alerts, notifications, loading states, and progress indicators
- **[Media Components](./components/media.md)** - Images, videos, galleries, and media handling

### 🔧 Advanced Components
- **[Chart Components](./components/charts.md)** - Analytics dashboards and data visualization
- **[Chat Components](./components/chat.md)** - Real-time messaging and conversation interfaces
- **[Admin Components](./components/admin.md)** - Administrative interfaces and management tools

## Design System Foundation

### Design Tokens

```css
:root {
  /* Colors */
  --color-primary-50: #eff6ff;
  --color-primary-100: #dbeafe;
  --color-primary-500: #3b82f6;
  --color-primary-900: #1e3a8a;
  
  /* Typography */
  --font-family-sans: 'Inter', sans-serif;
  --font-size-xs: 0.75rem;
  --font-size-sm: 0.875rem;
  --font-size-base: 1rem;
  --font-size-lg: 1.125rem;
  --font-size-xl: 1.25rem;
  
  /* Spacing */
  --space-1: 0.25rem;
  --space-2: 0.5rem;
  --space-4: 1rem;
  --space-6: 1.5rem;
  --space-8: 2rem;
  
  /* Shadows */
  --shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.05);
  --shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.1);
  --shadow-lg: 0 10px 15px -3px rgb(0 0 0 / 0.1);
  
  /* Border Radius */
  --radius-sm: 0.125rem;
  --radius-md: 0.375rem;
  --radius-lg: 0.5rem;
  --radius-xl: 0.75rem;
}
```

### Component Architecture

```typescript
// Base component interface
interface BaseComponentProps {
  className?: string;
  children?: React.ReactNode;
  'data-testid'?: string;
  size?: 'sm' | 'md' | 'lg';
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost';
  disabled?: boolean;
}

// Example: Button component
interface ButtonProps extends BaseComponentProps {
  type?: 'button' | 'submit' | 'reset';
  loading?: boolean;
  leftIcon?: React.ReactNode;
  rightIcon?: React.ReactNode;
  onClick?: (event: React.MouseEvent<HTMLButtonElement>) => void;
}

export const Button: React.FC<ButtonProps> = ({
  children,
  size = 'md',
  variant = 'primary',
  loading = false,
  disabled = false,
  className,
  leftIcon,
  rightIcon,
  ...props
}) => {
  const baseClasses = 'button';
  const sizeClasses = `button--${size}`;
  const variantClasses = `button--${variant}`;
  const stateClasses = loading ? 'button--loading' : '';
  
  return (
    <button
      className={cn(baseClasses, sizeClasses, variantClasses, stateClasses, className)}
      disabled={disabled || loading}
      {...props}
    >
      {loading ? <Spinner size="sm" /> : leftIcon}
      {children}
      {rightIcon}
    </button>
  );
};
```

## Component Examples

### Form Components

```typescript
// Input Component
export const Input: React.FC<InputProps> = ({
  label,
  error,
  helperText,
  required,
  ...props
}) => (
  <div className="input-group">
    {label && (
      <label className="input-label">
        {label}
        {required && <span className="input-required">*</span>}
      </label>
    )}
    <input
      className={cn('input', error && 'input--error')}
      aria-invalid={!!error}
      aria-describedby={error ? `${props.id}-error` : undefined}
      {...props}
    />
    {error && (
      <div id={`${props.id}-error`} className="input-error">
        {error}
      </div>
    )}
    {helperText && !error && (
      <div className="input-helper">{helperText}</div>
    )}
  </div>
);

// Usage Example
<Input
  id="email"
  label="Email Address"
  type="email"
  required
  placeholder="you@example.com"
  error={errors.email}
  helperText="We'll never share your email"
/>
```

### Data Display Components

```typescript
// Card Component
export const Card: React.FC<CardProps> = ({
  children,
  header,
  footer,
  padding = 'md',
  shadow = 'md',
  className,
  ...props
}) => (
  <div
    className={cn(
      'card',
      `card--padding-${padding}`,
      `card--shadow-${shadow}`,
      className
    )}
    {...props}
  >
    {header && <div className="card-header">{header}</div>}
    <div className="card-content">{children}</div>
    {footer && <div className="card-footer">{footer}</div>}
  </div>
);

// Table Component
export const Table: React.FC<TableProps> = ({
  columns,
  data,
  loading,
  emptyMessage = 'No data available',
  className,
}) => (
  <div className={cn('table-container', className)}>
    {loading ? (
      <div className="table-loading">
        <Spinner size="lg" />
        <span>Loading data...</span>
      </div>
    ) : data.length === 0 ? (
      <div className="table-empty">{emptyMessage}</div>
    ) : (
      <table className="table">
        <thead className="table-header">
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                className={cn('table-header-cell', column.align && `text-${column.align}`)}
              >
                {column.title}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="table-body">
          {data.map((row, index) => (
            <tr key={index} className="table-row">
              {columns.map((column) => (
                <td
                  key={column.key}
                  className={cn('table-cell', column.align && `text-${column.align}`)}
                >
                  {column.render ? column.render(row[column.key], row) : row[column.key]}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    )}
  </div>
);
```

### Feedback Components

```typescript
// Alert Component
export const Alert: React.FC<AlertProps> = ({
  children,
  type = 'info',
  title,
  closable = false,
  onClose,
  className,
  ...props
}) => {
  const [visible, setVisible] = useState(true);

  const handleClose = () => {
    setVisible(false);
    onClose?.();
  };

  if (!visible) return null;

  return (
    <div
      className={cn('alert', `alert--${type}`, className)}
      role="alert"
      {...props}
    >
      <div className="alert-icon">
        {type === 'success' && <CheckIcon />}
        {type === 'error' && <XCircleIcon />}
        {type === 'warning' && <ExclamationTriangleIcon />}
        {type === 'info' && <InformationCircleIcon />}
      </div>
      <div className="alert-content">
        {title && <div className="alert-title">{title}</div>}
        <div className="alert-message">{children}</div>
      </div>
      {closable && (
        <button
          className="alert-close"
          onClick={handleClose}
          aria-label="Close alert"
        >
          <XMarkIcon />
        </button>
      )}
    </div>
  );
};

// Progress Component
export const Progress: React.FC<ProgressProps> = ({
  value,
  max = 100,
  size = 'md',
  color = 'primary',
  showLabel = false,
  className,
}) => {
  const percentage = Math.min(100, Math.max(0, (value / max) * 100));

  return (
    <div className={cn('progress', `progress--${size}`, className)}>
      <div
        className={cn('progress-bar', `progress-bar--${color}`)}
        style={{ width: `${percentage}%` }}
        role="progressbar"
        aria-valuenow={value}
        aria-valuemin={0}
        aria-valuemax={max}
      />
      {showLabel && (
        <span className="progress-label">{Math.round(percentage)}%</span>
      )}
    </div>
  );
};
```

## Responsive Design Integration

### Responsive Utilities

```css
/* Responsive visibility utilities */
.hidden-sm { @media (max-width: 576px) { display: none !important; } }
.hidden-md { @media (max-width: 768px) { display: none !important; } }
.visible-lg { @media (min-width: 992px) { display: block !important; } }

/* Responsive spacing */
.p-responsive { padding: var(--space-4); }
@media (min-width: 768px) {
  .p-responsive { padding: var(--space-6); }
}
@media (min-width: 1024px) {
  .p-responsive { padding: var(--space-8); }
}

/* Responsive grid */
.grid {
  display: grid;
  gap: var(--space-4);
  grid-template-columns: 1fr;
}

@media (min-width: 768px) {
  .grid { grid-template-columns: repeat(2, 1fr); }
}

@media (min-width: 1024px) {
  .grid { grid-template-columns: repeat(3, 1fr); }
}
```

### Responsive Components

```typescript
// Responsive navigation component
export const Navigation: React.FC<NavigationProps> = ({
  items,
  logo,
  className,
}) => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const isMobile = useMediaQuery('(max-width: 768px)');

  return (
    <nav className={cn('navigation', className)}>
      <div className="navigation-container">
        <div className="navigation-brand">
          {logo}
        </div>

        {/* Desktop Navigation */}
        {!isMobile && (
          <div className="navigation-menu">
            {items.map((item) => (
              <a
                key={item.href}
                href={item.href}
                className="navigation-link"
              >
                {item.label}
              </a>
            ))}
          </div>
        )}

        {/* Mobile Menu Toggle */}
        {isMobile && (
          <button
            className="navigation-toggle"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            aria-label="Toggle mobile menu"
          >
            <MenuIcon />
          </button>
        )}
      </div>

      {/* Mobile Navigation */}
      {isMobile && mobileMenuOpen && (
        <div className="navigation-mobile">
          {items.map((item) => (
            <a
              key={item.href}
              href={item.href}
              className="navigation-mobile-link"
              onClick={() => setMobileMenuOpen(false)}
            >
              {item.label}
            </a>
          ))}
        </div>
      )}
    </nav>
  );
};
```

## Accessibility Features

### ARIA Implementation

```typescript
// Accessible dropdown component
export const Dropdown: React.FC<DropdownProps> = ({
  trigger,
  children,
  placement = 'bottom-start',
  className,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const triggerId = useId();
  const menuId = useId();

  return (
    <div className={cn('dropdown', className)}>
      <button
        id={triggerId}
        className="dropdown-trigger"
        aria-expanded={isOpen}
        aria-haspopup="true"
        aria-controls={menuId}
        onClick={() => setIsOpen(!isOpen)}
      >
        {trigger}
      </button>

      {isOpen && (
        <div
          id={menuId}
          className="dropdown-menu"
          role="menu"
          aria-labelledby={triggerId}
        >
          {children}
        </div>
      )}
    </div>
  );
};

// Accessible form validation
export const FormField: React.FC<FormFieldProps> = ({
  label,
  children,
  error,
  required,
  helperText,
}) => {
  const fieldId = useId();
  const errorId = useId();
  const helperId = useId();

  return (
    <div className="form-field">
      <label
        htmlFor={fieldId}
        className="form-label"
      >
        {label}
        {required && (
          <span className="form-required" aria-label="required">
            *
          </span>
        )}
      </label>

      {React.cloneElement(children as React.ReactElement, {
        id: fieldId,
        'aria-invalid': !!error,
        'aria-describedby': cn(
          error && errorId,
          helperText && helperId
        ),
      })}

      {error && (
        <div
          id={errorId}
          className="form-error"
          role="alert"
          aria-live="polite"
        >
          {error}
        </div>
      )}

      {helperText && !error && (
        <div id={helperId} className="form-helper">
          {helperText}
        </div>
      )}
    </div>
  );
};
```

## Testing & Quality Assurance

### Component Testing

```typescript
// Example component tests
import { render, screen, fireEvent } from '@testing-library/react';
import { Button } from './Button';

describe('Button Component', () => {
  it('renders with correct text', () => {
    render(<Button>Click me</Button>);
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument();
  });

  it('handles click events', () => {
    const handleClick = jest.fn();
    render(<Button onClick={handleClick}>Click me</Button>);
    
    fireEvent.click(screen.getByRole('button'));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('shows loading state', () => {
    render(<Button loading>Click me</Button>);
    expect(screen.getByRole('button')).toBeDisabled();
    expect(screen.getByTestId('spinner')).toBeInTheDocument();
  });

  it('applies correct variant classes', () => {
    render(<Button variant="secondary">Click me</Button>);
    expect(screen.getByRole('button')).toHaveClass('button--secondary');
  });
});
```

### Visual Regression Testing

```typescript
// Storybook configuration for visual testing
import type { Meta, StoryObj } from '@storybook/react';
import { Button } from './Button';

const meta: Meta<typeof Button> = {
  title: 'Components/Button',
  component: Button,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['primary', 'secondary', 'outline', 'ghost'],
    },
    size: {
      control: { type: 'select' },
      options: ['sm', 'md', 'lg'],
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

export const Primary: Story = {
  args: {
    children: 'Button',
    variant: 'primary',
  },
};

export const AllVariants: Story = {
  render: () => (
    <div className="flex gap-4">
      <Button variant="primary">Primary</Button>
      <Button variant="secondary">Secondary</Button>
      <Button variant="outline">Outline</Button>
      <Button variant="ghost">Ghost</Button>
    </div>
  ),
};
```

## Usage Guidelines

### Component Best Practices

1. **Consistency**: Always use design tokens for colors, spacing, and typography
2. **Accessibility**: Include proper ARIA attributes and keyboard navigation
3. **Responsiveness**: Ensure components work across all screen sizes
4. **Performance**: Optimize for minimal re-renders and efficient DOM updates
5. **Testing**: Write comprehensive tests for all component variations

### Implementation Examples

```typescript
// Good: Using design tokens and proper props
<Button
  variant="primary"
  size="lg"
  leftIcon={<PlusIcon />}
  onClick={handleSubmit}
>
  Create Project
</Button>

// Good: Accessible form implementation
<form onSubmit={handleSubmit}>
  <FormField
    label="Project Name"
    required
    error={errors.name}
  >
    <Input
      value={formData.name}
      onChange={(e) => setFormData({ ...formData, name: e.target.value })}
      placeholder="Enter project name"
    />
  </FormField>
  
  <Button type="submit" loading={isSubmitting}>
    Create Project
  </Button>
</form>

// Good: Responsive layout with proper spacing
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
  {projects.map((project) => (
    <Card key={project.id}>
      <h3>{project.name}</h3>
      <p>{project.description}</p>
    </Card>
  ))}
</div>
```

## Performance Considerations

### Optimization Strategies

```typescript
// Memoization for expensive computations
const ExpensiveComponent = memo(({ data }: { data: LargeDataSet }) => {
  const processedData = useMemo(() => {
    return data.map(item => complexProcessing(item));
  }, [data]);

  return <div>{/* Render processed data */}</div>;
});

// Virtual scrolling for large lists
import { FixedSizeList as List } from 'react-window';

const VirtualizedList: React.FC<{ items: any[] }> = ({ items }) => (
  <List
    height={600}
    itemCount={items.length}
    itemSize={50}
    itemData={items}
  >
    {({ index, style, data }) => (
      <div style={style}>
        <ListItem item={data[index]} />
      </div>
    )}
  </List>
);

// Lazy loading for images
const LazyImage: React.FC<{ src: string; alt: string }> = ({ src, alt }) => (
  <img
    src={src}
    alt={alt}
    loading="lazy"
    className="lazy-image"
    onLoad={(e) => e.currentTarget.classList.add('loaded')}
  />
);
```

## Related Documentation

- [Responsive Design Patterns](./responsive-design-patterns.md) - Mobile-first design methodology
- [CSS Development Guidelines](./css-development-guidelines.md) - CSS standards and best practices
- [Integration Examples](./examples/INTEGRATION-EXAMPLES.md) - Real-world component usage examples