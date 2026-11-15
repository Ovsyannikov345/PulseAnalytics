import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { StatTile } from './StatTile';

describe('StatTile', () => {
  it('shows the label, the value with its unit and the caption', () => {
    const { container } = render(
      <StatTile
        label="Average CO₂"
        status="ready"
        value="672"
        unit="ppm"
        caption="PM2.5 19 µg/m³ · humidity 45 %"
      />,
    );

    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'false');
    expect(container.firstElementChild).toHaveTextContent(
      'Average CO₂672 ppmPM2.5 19 µg/m³ · humidity 45 %',
    );
  });

  it('shows only the value when there is no unit or caption', () => {
    const { container } = render(<StatTile label="Samples, last 24 h" status="ready" value="—" />);

    expect(container.firstElementChild).toHaveTextContent(/^Samples, last 24 h—$/);
  });

  it('marks itself busy and keeps the label while loading', () => {
    const { container } = render(<StatTile label="Motion now" status="loading" />);

    expect(container.firstElementChild).toHaveAttribute('aria-busy', 'true');
    expect(container.firstElementChild).toHaveTextContent(/^Motion now$/);
  });

  it('shows an error with Retry', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(
      <StatTile
        label="Samples, last 24 h"
        status="error"
        message="Couldn't load the sample count."
        onRetry={onRetry}
      />,
    );

    expect(screen.getByRole('status')).toHaveTextContent("Couldn't load the sample count.");
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});
