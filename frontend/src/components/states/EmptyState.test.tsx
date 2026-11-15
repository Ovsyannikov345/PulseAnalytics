import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Button } from '../Button';
import { EmptyState } from './EmptyState';

describe('EmptyState', () => {
  it('shows the title as a heading with the description and actions', () => {
    render(
      <EmptyState
        title="No readings between 05:15 and 05:45"
        description="WeakApp may have been unavailable in this period."
      >
        <Button>Clear filters</Button>
      </EmptyState>,
    );

    expect(
      screen.getByRole('heading', { level: 3, name: 'No readings between 05:15 and 05:45' }),
    ).toBeInTheDocument();
    expect(
      screen.getByText('WeakApp may have been unavailable in this period.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Clear filters' })).toBeInTheDocument();
  });

  it('shows only the title when nothing else is given', () => {
    const { container } = render(<EmptyState title="No readings" />);

    expect(screen.getByRole('heading', { name: 'No readings' })).toBeInTheDocument();
    expect(container.querySelector('p')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
  });
});
