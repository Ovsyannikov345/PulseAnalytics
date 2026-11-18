import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Chip } from './Chip';

describe('Chip', () => {
  it.each(['motion', 'warning'] as const)('adds a hidden icon to the %s label', (variant) => {
    const { container } = render(<Chip variant={variant}>Label</Chip>);

    expect(container.firstElementChild).toHaveTextContent('Label');
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  });

  it.each([
    ['neutral', 'No motion'],
    ['waiting', 'Motion: waiting'],
  ] as const)('shows a %s label without an icon', (variant, label) => {
    const { container } = render(<Chip variant={variant}>{label}</Chip>);

    expect(container.firstElementChild).toHaveTextContent(label);
    expect(container.querySelector('svg')).toBeNull();
  });
});
