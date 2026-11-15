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

  it('shows neither "Updating…" nor a dimmer unless it can refresh', () => {
    render(
      <Card title="Energy">
        <span>Chart</span>
      </Card>,
    );

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('keeps the content visible while it refreshes, dimmed under "Updating…"', () => {
    render(
      <Card title="Energy" refreshing>
        <span>Chart</span>
      </Card>,
    );

    expect(screen.getByRole('status')).toHaveTextContent('Updating…');
    expect(screen.getByRole('progressbar', { name: 'Updating' })).toBeInTheDocument();
    expect(screen.getByText('Chart')).toBeVisible();
  });

  it('wraps refreshable content without "Updating…" when the data is current', () => {
    render(
      <Card title="Energy" refreshing={false}>
        <span>Chart</span>
      </Card>,
    );

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.getByText('Chart').closest('[aria-busy]')).toHaveAttribute('aria-busy', 'false');
  });

  it('shows actions in the header and can use a level 3 heading', () => {
    render(
      <Card title="CO₂" headingLevel={3} actions={<button type="button">Combined</button>}>
        <span>Chart</span>
      </Card>,
    );

    expect(screen.getByRole('heading', { level: 3, name: 'CO₂' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Combined' })).toBeVisible();
  });
});
