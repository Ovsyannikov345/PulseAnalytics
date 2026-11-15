import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Chip } from './Chip';

describe('Chip', () => {
  it.each(['motion', 'warning'] as const)('adds a hidden icon to the %s label', (variant) => {
    const { container } = render(<Chip variant={variant}>Label</Chip>);

    expect(container.firstElementChild).toHaveTextContent('Label');
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  });

  it('shows a neutral label without an icon', () => {
    const { container } = render(<Chip variant="neutral">No motion</Chip>);

    expect(container.firstElementChild).toHaveTextContent('No motion');
    expect(container.querySelector('svg')).toBeNull();
  });
});
