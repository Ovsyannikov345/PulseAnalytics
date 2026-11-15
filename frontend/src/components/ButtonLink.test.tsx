import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ButtonLink } from './ButtonLink';

describe('ButtonLink', () => {
  it('is a link with its attributes', () => {
    render(
      <ButtonLink href="/api/readings/export" download variant="outline" className="extra">
        Export CSV
      </ButtonLink>,
    );

    const link = screen.getByRole('link', { name: 'Export CSV' });
    expect(link).toHaveAttribute('href', '/api/readings/export');
    expect(link).toHaveAttribute('download');
    expect(link).toHaveClass('extra');
  });

  it('defaults to the secondary look', () => {
    render(<ButtonLink href="/readings">Readings</ButtonLink>);

    expect(screen.getByRole('link', { name: 'Readings' }).className).toMatch(/secondary/);
  });
});
