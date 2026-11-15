import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Card } from './Card';

describe('Card', () => {
  it('is a region named by its heading, with the description and content', () => {
    render(
      <Card title="Energy, last 24 hours" description="Average of all locations per 15 minutes">
        <p>Chart</p>
      </Card>,
    );

    const region = screen.getByRole('region', { name: 'Energy, last 24 hours' });
    expect(screen.getByRole('heading', { level: 2, name: 'Energy, last 24 hours' })).toBeVisible();
    expect(region).toHaveTextContent('Average of all locations per 15 minutes');
    expect(region).toHaveTextContent('Chart');
    expect(region).toHaveAttribute('aria-busy', 'false');
  });

  it('can be marked busy while its content loads', () => {
    render(
      <Card title="Energy, last 24 hours" busy>
        <span>Skeleton</span>
      </Card>,
    );

    expect(screen.getByRole('region', { name: 'Energy, last 24 hours' })).toHaveAttribute(
      'aria-busy',
      'true',
    );
  });

  it('leaves out the description when none is given', () => {
    const { container } = render(
      <Card title="Ingestion coverage">
        <span>Bars</span>
      </Card>,
    );

    expect(container.querySelector('p')).toBeNull();
  });
});
